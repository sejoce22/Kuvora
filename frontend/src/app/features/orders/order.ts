import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { formatClp } from '../../core/format/clp';
import { problemMessage } from '../../core/http/problem-details';
import { Category, Product } from '../catalog/catalog.models';
import { CatalogService } from '../catalog/catalog.service';
import { NOTE_MAX, Order, OrderItem, QUANTITY_MAX, REASON_MAX } from './orders.models';
import { OrdersService } from './orders.service';

// Notas de un toque; más adelante serán configurables por restaurante.
const QUICK_NOTES = ['Sin cebolla', 'Sin cilantro', 'Bien cocido', 'Sin sal', 'Para llevar'];

const TOAST_MS = 6000;

type CancelTarget = { kind: 'order' } | { kind: 'item'; item: OrderItem };

interface Round {
  round: number;
  sentAtUtc: string | null;
  items: OrderItem[];
}

interface ProductGroup {
  category: Category;
  products: Product[];
}

// Búsqueda sin tildes ni mayúsculas: "cafe" encuentra "Café".
const normalize = (text: string): string => text.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();

const notBlank = (control: AbstractControl<string>): ValidationErrors | null =>
  control.value.trim() === '' ? { required: true } : null;

@Component({
  selector: 'app-order',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './order.html',
  styleUrl: './order.scss',
})
export class OrderScreen implements OnInit {
  private readonly orders = inject(OrdersService);
  private readonly catalog = inject(CatalogService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly orderId = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';

  protected readonly order = signal<Order | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly categories = signal<Category[]>([]);
  protected readonly products = signal<Product[]>([]);
  protected readonly catalogFailed = signal(false);

  // Mientras una operación está en curso se ignoran los toques: evita dos escrituras a la vez sobre el mismo pedido.
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly search = signal('');
  protected readonly categoryFilter = signal<string | null>(null);
  // Comensal al que se asignan los productos que se toquen (null = compartido)
  protected readonly diner = signal<number | null>(null);
  protected readonly noteOpenId = signal<string | null>(null);

  protected readonly cancelTarget = signal<CancelTarget | null>(null);
  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: [notBlank, Validators.maxLength(REASON_MAX)],
  });

  protected readonly justSentRound = signal<number | null>(null);
  protected readonly toast = signal<{ message: string; undo?: () => void } | null>(null);
  private toastTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  protected readonly quickNotes = QUICK_NOTES;
  protected readonly noteMax = NOTE_MAX;
  protected readonly reasonMax = REASON_MAX;
  protected readonly formatClp = formatClp;

  protected readonly isAdmin = computed(() => this.auth.user()?.role === 'Admin');
  protected readonly isOpen = computed(() => this.order()?.status === 'Open');

  // Fichas de comensal: una por persona del pedido (en mostrador no hay)
  protected readonly guestChips = computed(() =>
    Array.from({ length: this.order()?.guestCount ?? 0 }, (_, index) => index + 1),
  );

  protected readonly pendingItems = computed(
    () => this.order()?.items.filter((item) => item.status === 'Pending') ?? [],
  );

  protected readonly rounds = computed<Round[]>(() => {
    const byRound = new Map<number, Round>();

    for (const item of this.order()?.items ?? []) {
      if (item.status !== 'Sent' || item.round === null) continue;
      const group = byRound.get(item.round) ?? { round: item.round, sentAtUtc: item.sentAtUtc, items: [] };
      group.items.push(item);
      byRound.set(item.round, group);
    }

    return [...byRound.values()].sort((a, b) => a.round - b.round);
  });

  protected readonly cancelledItems = computed(
    () => this.order()?.items.filter((item) => item.status === 'Cancelled') ?? [],
  );

  protected readonly hasSent = computed(() => this.order()?.items.some((item) => item.status === 'Sent') ?? false);

  // El mesero cancela mientras nada haya salido a cocina; después, solo el Admin (el backend lo exige igual).
  protected readonly canCancelOrder = computed(() => this.isOpen() && (this.isAdmin() || !this.hasSent()));

  protected readonly groups = computed<ProductGroup[]>(() => {
    const query = normalize(this.search().trim());
    const categoryId = this.categoryFilter();

    const visible = this.products().filter(
      (product) =>
        (categoryId === null || product.categoryId === categoryId) &&
        (query === '' || normalize(product.name).includes(query) || normalize(product.categoryName).includes(query)),
    );

    return this.categories()
      .map((category) => ({ category, products: visible.filter((p) => p.categoryId === category.id) }))
      .filter((group) => group.products.length > 0);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.toastTimer));
  }

  ngOnInit(): void {
    this.loadOrder();
    this.loadCatalog();
  }

  // ---- Carga ----

  protected loadOrder(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.orders.get(this.orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.loadError.set(
          error.status === 404 ? 'No encontramos este pedido.' : 'No pudimos cargar el pedido.',
        );
      },
    });
  }

  protected loadCatalog(): void {
    this.catalogFailed.set(false);

    forkJoin([this.catalog.getCategories(false), this.catalog.getProducts(false)]).subscribe({
      next: ([categories, products]) => {
        this.categories.set([...categories].sort((a, b) => a.sortOrder - b.sortOrder));
        this.products.set(products);
      },
      error: () => this.catalogFailed.set(true),
    });
  }

  // ---- Carta ----

  protected setSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  // Enter en el buscador agrega el primer resultado
  protected addFirst(): void {
    const first = this.groups()[0]?.products[0];
    if (first) this.addProduct(first);
  }

  protected addProduct(product: Product): void {
    const order = this.order();
    if (!order || !this.isOpen() || this.busy()) return;

    // Mismo producto, mismo comensal y sin nota: se suma a la línea que ya está pendiente.
    const existing = this.pendingItems().find(
      (item) => item.productId === product.id && item.dinerNumber === this.diner() && item.note === null,
    );

    if (existing) {
      if (existing.quantity >= QUANTITY_MAX) return;
      this.changeItem(existing, { quantity: existing.quantity + 1 });
      return;
    }

    this.run(
      this.orders.addItem(order.id, { productId: product.id, quantity: 1, dinerNumber: this.diner() }),
    );
  }

  // ---- Comanda en edición ----

  protected changeQuantity(item: OrderItem, delta: number): void {
    const quantity = item.quantity + delta;
    if (quantity < 1) {
      this.remove(item);
      return;
    }
    if (quantity > QUANTITY_MAX || this.busy()) return;
    this.changeItem(item, { quantity });
  }

  protected onDinerChange(item: OrderItem, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.changeItem(item, { dinerNumber: value === '' ? null : Number(value) });
  }

  protected toggleNoteEditor(item: OrderItem): void {
    this.noteOpenId.update((open) => (open === item.id ? null : item.id));
  }

  protected hasNote(item: OrderItem, text: string): boolean {
    return this.noteParts(item).includes(text);
  }

  protected toggleQuickNote(item: OrderItem, text: string): void {
    if (this.busy()) return;

    const parts = this.noteParts(item);
    const next = parts.includes(text) ? parts.filter((part) => part !== text) : [...parts, text];
    const note = next.join(', ');

    if (note.length > NOTE_MAX) {
      this.actionError.set(`La nota no puede superar los ${NOTE_MAX} caracteres.`);
      return;
    }

    this.changeItem(item, { note: note === '' ? null : note });
  }

  protected saveNote(item: OrderItem, event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    const note = value === '' ? null : value;
    if (note === item.note || this.busy()) return;
    this.changeItem(item, { note });
  }

  // Quitar no pide confirmación: avisa con "Deshacer", que vuelve a agregar la misma línea.
  protected remove(item: OrderItem): void {
    const order = this.order();
    if (!order || this.busy()) return;

    this.run(this.orders.removeItem(order.id, item.id), () =>
      this.showToast(`Quitaste ${item.productName}`, () => this.restore(item)),
    );
  }

  private restore(item: OrderItem): void {
    const order = this.order();
    if (!order || !this.isOpen() || this.busy()) return;

    this.toast.set(null);
    this.run(
      this.orders.addItem(order.id, {
        productId: item.productId,
        quantity: item.quantity,
        note: item.note,
        dinerNumber: item.dinerNumber,
      }),
    );
  }

  protected send(): void {
    const order = this.order();
    if (!order || !this.isOpen() || this.busy() || this.pendingItems().length === 0) return;

    this.run(this.orders.sendToKitchen(order.id), (updated) => {
      this.justSentRound.set(updated.currentRound);
      this.showToast(`Ronda ${updated.currentRound} enviada a cocina`);
    });
  }

  // ---- Cancelaciones (con motivo) ----

  protected askCancelOrder(): void {
    this.openCancel({ kind: 'order' });
  }

  protected askCancelItem(item: OrderItem): void {
    this.openCancel({ kind: 'item', item });
  }

  protected closeCancel(): void {
    this.cancelTarget.set(null);
  }

  protected confirmCancel(): void {
    const order = this.order();
    const target = this.cancelTarget();
    if (!order || !target || this.busy()) return;

    if (this.reason.invalid) {
      this.reason.markAsTouched();
      return;
    }

    const request = { reason: this.reason.value.trim() };

    if (target.kind === 'order') {
      this.run(this.orders.cancelOrder(order.id, request), () => void this.router.navigate(['/mesas']));
    } else {
      this.run(this.orders.cancelItem(order.id, target.item.id, request), () => this.closeCancel());
    }
  }

  // ---- Atajos de teclado ----

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
      event.preventDefault();
      this.send();
      return;
    }

    if (event.key === '/' && !this.isTyping(event.target)) {
      event.preventDefault();
      this.searchInput()?.nativeElement.focus();
    }
  }

  // ---- Internos ----

  private isTyping(target: EventTarget | null): boolean {
    return target instanceof HTMLElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
  }

  private noteParts(item: OrderItem): string[] {
    return (item.note ?? '')
      .split(',')
      .map((part) => part.trim())
      .filter((part) => part !== '');
  }

  private changeItem(
    item: OrderItem,
    patch: { quantity?: number; note?: string | null; dinerNumber?: number | null },
  ): void {
    const order = this.order();
    if (!order) return;

    this.run(
      this.orders.updateItem(order.id, item.id, {
        quantity: patch.quantity ?? item.quantity,
        note: patch.note !== undefined ? patch.note : item.note,
        dinerNumber: patch.dinerNumber !== undefined ? patch.dinerNumber : item.dinerNumber,
      }),
    );
  }

  private openCancel(target: CancelTarget): void {
    this.reason.reset('');
    this.actionError.set(null);
    this.cancelTarget.set(target);
  }

  private showToast(message: string, undo?: () => void): void {
    clearTimeout(this.toastTimer);
    this.toast.set({ message, undo });
    this.toastTimer = setTimeout(() => this.toast.set(null), TOAST_MS);
  }

  private run(call: Observable<Order>, onSuccess?: (order: Order) => void): void {
    this.busy.set(true);
    this.actionError.set(null);

    call.subscribe({
      next: (order) => {
        this.order.set(order);
        this.busy.set(false);
        onSuccess?.(order);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.actionError.set(problemMessage(error, 'No pudimos guardar el cambio.'));
        // Si otra persona cambió el pedido antes, se vuelve a leer para mostrar lo que hay de verdad.
        this.orders.get(this.orderId).subscribe({ next: (fresh) => this.order.set(fresh) });
      },
    });
  }
}
