import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { formatClp } from '../../core/format/clp';
import { problemMessage } from '../../core/http/problem-details';
import {
  CATEGORY_NAME_MAX,
  Category,
  PRODUCT_DESCRIPTION_MAX,
  PRODUCT_NAME_MAX,
  PRODUCT_PRICE_MAX,
  Product,
} from './catalog.models';
import { CatalogService } from './catalog.service';

// Qué formulario está abierto y sobre qué registro (id null = uno nuevo).
type Editor = { kind: 'category'; id: string | null } | { kind: 'product'; id: string | null };

@Component({
  selector: 'app-catalog',
  imports: [ReactiveFormsModule],
  templateUrl: './catalog.html',
  styleUrl: './catalog.scss',
})
export class Catalog implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly categories = signal<Category[]>([]);
  protected readonly products = signal<Product[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  // null = "Todas"
  protected readonly selectedCategoryId = signal<string | null>(null);
  protected readonly editor = signal<Editor | null>(null);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  // La interfaz solo muestra los botones de edición al Admin; la seguridad real está en el backend (403).
  protected readonly canEdit = computed(() => this.auth.user()?.role === 'Admin');

  protected readonly selectedCategory = computed(
    () => this.categories().find((category) => category.id === this.selectedCategoryId()) ?? null,
  );

  protected readonly visibleProducts = computed(() => {
    const categoryId = this.selectedCategoryId();
    return categoryId === null
      ? this.products()
      : this.products().filter((product) => product.categoryId === categoryId);
  });

  // Un producto solo se puede crear o mover a una categoría activa
  protected readonly activeCategories = computed(() => this.categories().filter((c) => c.isActive));

  protected readonly categoryNameMax = CATEGORY_NAME_MAX;
  protected readonly productNameMax = PRODUCT_NAME_MAX;
  protected readonly productDescriptionMax = PRODUCT_DESCRIPTION_MAX;
  protected readonly formatPrice = formatClp;

  protected readonly categoryForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(CATEGORY_NAME_MAX)]],
    sortOrder: [0, [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)]],
  });

  protected readonly productForm = this.formBuilder.nonNullable.group({
    categoryId: ['', [Validators.required]],
    name: ['', [Validators.required, Validators.maxLength(PRODUCT_NAME_MAX)]],
    description: ['', [Validators.maxLength(PRODUCT_DESCRIPTION_MAX)]],
    price: [
      0,
      [Validators.required, Validators.min(0), Validators.max(PRODUCT_PRICE_MAX), Validators.pattern(/^\d+$/)],
    ],
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.fetch(() => this.loading.set(false), () => this.failed.set(true));
  }

  protected select(categoryId: string | null): void {
    this.selectedCategoryId.set(categoryId);
  }

  // ---- Abrir y cerrar formularios ----

  protected newCategory(): void {
    this.categoryForm.reset({ name: '', sortOrder: this.categories().length });
    this.openEditor({ kind: 'category', id: null });
  }

  protected editCategory(category: Category): void {
    this.categoryForm.reset({ name: category.name, sortOrder: category.sortOrder });
    this.openEditor({ kind: 'category', id: category.id });
  }

  protected newProduct(): void {
    const selected = this.selectedCategory();
    const categoryId = selected?.isActive ? selected.id : (this.activeCategories()[0]?.id ?? '');

    this.productForm.reset({ categoryId, name: '', description: '', price: 0 });
    this.openEditor({ kind: 'product', id: null });
  }

  protected editProduct(product: Product): void {
    this.productForm.reset({
      categoryId: product.categoryId,
      name: product.name,
      description: product.description ?? '',
      price: product.price,
    });
    this.openEditor({ kind: 'product', id: product.id });
  }

  protected cancel(): void {
    this.editor.set(null);
    this.formError.set(null);
  }

  // ---- Guardar ----

  protected saveCategory(): void {
    const current = this.editor();
    if (this.categoryForm.invalid || current?.kind !== 'category') {
      this.categoryForm.markAllAsTouched();
      return;
    }

    const request = this.categoryForm.getRawValue();
    const call =
      current.id === null
        ? this.catalog.createCategory(request)
        : this.catalog.updateCategory(current.id, request);

    this.saving.set(true);
    this.formError.set(null);

    call.subscribe({
      next: () => this.finishSaving(),
      error: (error: HttpErrorResponse) => this.failSaving(error, 'No pudimos guardar la categoría.'),
    });
  }

  protected saveProduct(): void {
    const current = this.editor();
    if (this.productForm.invalid || current?.kind !== 'product') {
      this.productForm.markAllAsTouched();
      return;
    }

    const { description, ...rest } = this.productForm.getRawValue();
    // Descripción vacía = sin descripción (null), como espera la API
    const request = { ...rest, description: description.trim() === '' ? null : description };
    const call =
      current.id === null
        ? this.catalog.createProduct(request)
        : this.catalog.updateProduct(current.id, request);

    this.saving.set(true);
    this.formError.set(null);

    call.subscribe({
      next: () => {
        // Para ver el producto recién guardado, se muestra su categoría
        this.selectedCategoryId.set(request.categoryId);
        this.finishSaving();
      },
      error: (error: HttpErrorResponse) => this.failSaving(error, 'No pudimos guardar el producto.'),
    });
  }

  // ---- Activar y desactivar ----

  protected toggleCategory(category: Category): void {
    this.actionError.set(null);
    this.catalog.setCategoryActive(category.id, !category.isActive).subscribe({
      next: () => this.refresh(),
      // Ej.: 409 "No puedes desactivar una categoría que tiene productos activos."
      error: (error: HttpErrorResponse) =>
        this.actionError.set(problemMessage(error, 'No pudimos cambiar el estado de la categoría.')),
    });
  }

  protected toggleProduct(product: Product): void {
    this.actionError.set(null);
    this.catalog.setProductActive(product.id, !product.isActive).subscribe({
      next: () => this.refresh(),
      // Ej.: 409 al activar un producto cuya categoría está inactiva
      error: (error: HttpErrorResponse) =>
        this.actionError.set(problemMessage(error, 'No pudimos cambiar el estado del producto.')),
    });
  }

  // ---- Internos ----

  private openEditor(editor: Editor): void {
    this.formError.set(null);
    this.actionError.set(null);
    this.editor.set(editor);
  }

  private finishSaving(): void {
    this.saving.set(false);
    this.cancel();
    this.refresh();
  }

  private failSaving(error: HttpErrorResponse, fallback: string): void {
    this.saving.set(false);
    this.formError.set(problemMessage(error, fallback));
  }

  // Recarga en silencio (sin el aviso "Cargando…") después de guardar o cambiar un estado
  private refresh(): void {
    this.fetch(
      () => undefined,
      () => this.actionError.set('No pudimos actualizar la lista. Recarga la página.'),
    );
  }

  // Categorías y productos se piden a la vez. El Admin también pide los inactivos.
  private fetch(onDone: () => void, onError: () => void): void {
    const includeInactive = this.canEdit();

    forkJoin({
      categories: this.catalog.getCategories(includeInactive),
      products: this.catalog.getProducts(includeInactive),
    }).subscribe({
      next: ({ categories, products }) => {
        this.categories.set(categories);
        this.products.set(products);
        onDone();
      },
      error: () => {
        onError();
        onDone();
      },
    });
  }
}
