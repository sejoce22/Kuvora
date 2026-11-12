import { HttpErrorResponse } from '@angular/common/http';
import { NgTemplateOutlet } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Role } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { formatClp } from '../../core/format/clp';
import { formatElapsed } from '../../core/format/elapsed';
import { problemMessage } from '../../core/http/problem-details';
import { GUEST_COUNT_MAX, GUEST_COUNT_MIN, Order, OrderSummary } from '../orders/orders.models';
import { OrdersService } from '../orders/orders.service';
import {
  DiningTable,
  TABLE_MAX_CAPACITY,
  TABLE_MIN_CAPACITY,
  TABLE_NAME_MAX,
  TABLE_ZONE_MAX,
  ZoneGroup,
} from './tables.models';
import { TablesService } from './tables.service';

// Orden "natural": Mesa 2 antes que Mesa 10 (el backend ordena como texto y deja Mesa 10 antes).
const collator = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });

// Quienes pueden abrir pedidos (política ManageOrders del backend). Cocina ve las mesas, pero no sus pedidos.
const ORDER_ROLES: readonly Role[] = ['Admin', 'Waiter', 'Cashier'];

// Mientras no haya tiempo real (Fase 5), el estado de la sala se refresca solo cada 30 segundos.
const REFRESH_MS = 30_000;

@Component({
  selector: 'app-tables',
  imports: [ReactiveFormsModule, NgTemplateOutlet],
  templateUrl: './tables.html',
  styleUrl: './tables.scss',
})
export class Tables implements OnInit {
  private readonly tablesService = inject(TablesService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly ordersService = inject(OrdersService);
  private readonly router = inject(Router);

  protected readonly tables = signal<DiningTable[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  // undefined = formulario cerrado; null = mesa nueva; string = id de la mesa que se edita
  protected readonly editingId = signal<string | null | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  // ---- Pedidos abiertos (estado de la sala) ----
  protected readonly openOrders = signal<OrderSummary[]>([]);
  protected readonly ordersLoaded = signal(false);
  protected readonly ordersFailed = signal(false);
  // Reloj que se actualiza solo: hace avanzar el "hace 12 min" sin recargar nada.
  protected readonly now = signal(Date.now());

  // Mesa en la que se está abriendo un pedido (pregunta cuántas personas son)
  protected readonly opening = signal<DiningTable | null>(null);
  protected readonly openingBusy = signal(false);
  protected readonly openingError = signal<string | null>(null);
  protected readonly counterBusy = signal(false);

  protected readonly canOrder = computed(() => {
    const role = this.auth.user()?.role;
    return role !== undefined && ORDER_ROLES.includes(role);
  });

  // Pedido abierto de cada mesa (hay como máximo uno por mesa)
  private readonly orderByTable = computed(
    () => new Map(this.openOrders().flatMap((o) => (o.tableId ? [[o.tableId, o] as const] : []))),
  );

  protected readonly counterOrders = computed(() => this.openOrders().filter((o) => o.tableId === null));

  // La interfaz solo muestra los botones de edición al Admin; la seguridad real está en el backend (403).
  protected readonly canEdit = computed(() => this.auth.user()?.role === 'Admin');

  // Mesas agrupadas por zona: zonas en orden alfabético ("Sin zona" al final) y mesas en orden natural.
  protected readonly zones = computed<ZoneGroup[]>(() => {
    const groups = new Map<string | null, DiningTable[]>();

    for (const table of this.tables()) {
      const key = table.zone?.trim() || null;
      const list = groups.get(key);
      if (list) {
        list.push(table);
      } else {
        groups.set(key, [table]);
      }
    }

    return [...groups.entries()]
      .map(([zone, tables]) => ({
        zone,
        tables: [...tables].sort((a, b) => collator.compare(a.name, b.name)),
      }))
      .sort((a, b) => {
        if (a.zone === null) return 1;
        if (b.zone === null) return -1;
        return collator.compare(a.zone, b.zone);
      });
  });

  // Zonas que ya existen: se ofrecen como sugerencia al escribir una zona (evita "Terraza" y "terraza ")
  protected readonly zoneNames = computed(() =>
    this.zones()
      .map((group) => group.zone)
      .filter((zone): zone is string => zone !== null),
  );

  protected readonly formatClp = formatClp;
  protected readonly guestMin = GUEST_COUNT_MIN;
  protected readonly guestMax = GUEST_COUNT_MAX;
  protected readonly tableNameMax = TABLE_NAME_MAX;
  protected readonly tableZoneMax = TABLE_ZONE_MAX;
  protected readonly minCapacity = TABLE_MIN_CAPACITY;
  protected readonly maxCapacity = TABLE_MAX_CAPACITY;

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(TABLE_NAME_MAX)]],
    capacity: [
      4,
      [
        Validators.required,
        Validators.min(TABLE_MIN_CAPACITY),
        Validators.max(TABLE_MAX_CAPACITY),
        Validators.pattern(/^\d+$/),
      ],
    ],
    zone: ['', [Validators.maxLength(TABLE_ZONE_MAX)]],
  });

  protected readonly guestsForm = this.formBuilder.nonNullable.group({
    guests: [
      2,
      [
        Validators.required,
        Validators.min(GUEST_COUNT_MIN),
        Validators.max(GUEST_COUNT_MAX),
        Validators.pattern(/^\d+$/),
      ],
    ],
  });

  constructor() {
    const timer = setInterval(() => {
      this.now.set(Date.now());
      this.loadOrders();
    }, REFRESH_MS);

    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  ngOnInit(): void {
    this.load();
    this.loadOrders();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.fetch(() => this.loading.set(false), () => this.failed.set(true));
  }

  // ---- Pedidos ----

  protected orderOf(tableId: string): OrderSummary | undefined {
    return this.orderByTable().get(tableId);
  }

  protected elapsed(order: OrderSummary): string {
    return formatElapsed(order.openedAtUtc, this.now());
  }

  protected loadOrders(): void {
    // Cocina no puede ver pedidos (403): ni siquiera se pide.
    if (!this.canOrder()) return;

    this.ordersService.getOpen().subscribe({
      next: (orders) => {
        this.openOrders.set(orders);
        this.ordersLoaded.set(true);
        this.ordersFailed.set(false);
      },
      // Si falla una actualización en silencio no se borra lo que ya se mostraba.
      error: () => this.ordersFailed.set(true),
    });
  }

  // Mesa ocupada: va a su pedido. Mesa libre: pregunta cuántas personas son.
  protected select(table: DiningTable): void {
    const order = this.orderOf(table.id);
    if (order) {
      void this.router.navigate(['/pedidos', order.id]);
      return;
    }

    this.actionError.set(null);
    this.openingError.set(null);
    this.editingId.set(undefined);
    this.guestsForm.reset({ guests: Math.min(table.capacity, 2) });
    this.opening.set(table);
  }

  protected cancelOpening(): void {
    this.opening.set(null);
    this.openingError.set(null);
  }

  protected openOrder(): void {
    const table = this.opening();
    if (!table) return;

    if (this.guestsForm.invalid) {
      this.guestsForm.markAllAsTouched();
      return;
    }

    this.openingBusy.set(true);
    this.openingError.set(null);

    this.ordersService
      .open({ channel: 'DineIn', tableId: table.id, guestCount: this.guestsForm.getRawValue().guests })
      .subscribe({
        next: (order) => this.goTo(order, () => this.openingBusy.set(false)),
        error: (error: HttpErrorResponse) => {
          this.openingBusy.set(false);
          this.openingError.set(problemMessage(error, 'No pudimos abrir el pedido.'));
          // Si otra persona ocupó la mesa justo antes, la sala se actualiza para mostrarlo.
          this.loadOrders();
        },
      });
  }

  protected openCounterOrder(): void {
    this.actionError.set(null);
    this.counterBusy.set(true);

    this.ordersService.open({ channel: 'Counter' }).subscribe({
      next: (order) => this.goTo(order, () => this.counterBusy.set(false)),
      error: (error: HttpErrorResponse) => {
        this.counterBusy.set(false);
        this.actionError.set(problemMessage(error, 'No pudimos abrir el pedido.'));
      },
    });
  }

  protected openCounterDetail(order: OrderSummary): void {
    void this.router.navigate(['/pedidos', order.id]);
  }

  private goTo(order: Order, done: () => void): void {
    done();
    void this.router.navigate(['/pedidos', order.id]);
  }

  // ---- Formulario ----

  protected newTable(): void {
    this.form.reset({ name: '', capacity: 4, zone: '' });
    this.openEditor(null);
  }

  protected editTable(table: DiningTable): void {
    this.form.reset({ name: table.name, capacity: table.capacity, zone: table.zone ?? '' });
    this.openEditor(table.id);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
    this.formError.set(null);
  }

  protected save(): void {
    const id = this.editingId();
    if (this.form.invalid || id === undefined) {
      this.form.markAllAsTouched();
      return;
    }

    const { zone, ...rest } = this.form.getRawValue();
    // Zona vacía = sin zona (null), como espera la API
    const request = { ...rest, zone: zone.trim() === '' ? null : zone.trim() };
    const call = id === null ? this.tablesService.create(request) : this.tablesService.update(id, request);

    this.saving.set(true);
    this.formError.set(null);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.refresh();
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.formError.set(problemMessage(error, 'No pudimos guardar la mesa.'));
      },
    });
  }

  protected toggle(table: DiningTable): void {
    this.actionError.set(null);
    this.tablesService.setActive(table.id, !table.isActive).subscribe({
      next: () => this.refresh(),
      error: (error: HttpErrorResponse) =>
        this.actionError.set(problemMessage(error, 'No pudimos cambiar el estado de la mesa.')),
    });
  }

  // ---- Internos ----

  private openEditor(id: string | null): void {
    this.formError.set(null);
    this.actionError.set(null);
    this.opening.set(null);
    this.editingId.set(id);
  }

  // Recarga en silencio (sin el aviso "Cargando…") después de guardar o cambiar un estado
  private refresh(): void {
    this.fetch(
      () => undefined,
      () => this.actionError.set('No pudimos actualizar la lista. Recarga la página.'),
    );
  }

  // El Admin también pide las mesas inactivas.
  private fetch(onDone: () => void, onError: () => void): void {
    this.tablesService.getAll(this.canEdit()).subscribe({
      next: (tables) => {
        this.tables.set(tables);
        onDone();
      },
      error: () => {
        onError();
        onDone();
      },
    });
  }
}
