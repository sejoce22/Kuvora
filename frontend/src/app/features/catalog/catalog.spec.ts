import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Role, User } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { Catalog } from './catalog';
import { Category, Product } from './catalog.models';

describe('Catalog', () => {
  const categories: Category[] = [
    { id: 'c1', name: 'Bebidas', sortOrder: 1, isActive: true },
    { id: 'c2', name: 'Postres', sortOrder: 2, isActive: true },
  ];

  const products: Product[] = [
    { id: 'p1', categoryId: 'c1', categoryName: 'Bebidas', name: 'Jugo de frambuesa', description: 'Natural', price: 3500, isActive: true },
    { id: 'p2', categoryId: 'c2', categoryName: 'Postres', name: 'Helado artesanal', description: null, price: 2500, isActive: true },
  ];

  let controller: HttpTestingController;

  // Crea la pantalla con un usuario del rol indicado (el AuthService real no hace falta para esto).
  function create(role: Role): ComponentFixture<Catalog> {
    const user: User = { id: 'u1', tenantId: 't1', email: 'ana@kuvora.test', fullName: 'Ana Pérez', role };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user: signal(user) } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Catalog);
    fixture.detectChanges();
    return fixture;
  }

  async function loadAs(role: Role, query = ''): Promise<ComponentFixture<Catalog>> {
    const fixture = create(role);
    controller.expectOne(`/api/categories${query}`).flush(categories);
    controller.expectOne(`/api/products${query}`).flush(products);
    await fixture.whenStable();
    return fixture;
  }

  const element = (fixture: ComponentFixture<Catalog>) => fixture.nativeElement as HTMLElement;
  const text = (fixture: ComponentFixture<Catalog>) => element(fixture).textContent ?? '';

  function buttonByText(fixture: ComponentFixture<Catalog>, label: string): HTMLButtonElement {
    const button = Array.from(element(fixture).querySelectorAll('button')).find((b) =>
      b.textContent?.includes(label),
    );
    if (!button) throw new Error(`No hay un botón con el texto "${label}"`);
    return button;
  }

  function type(fixture: ComponentFixture<Catalog>, selector: string, value: string): void {
    const input = element(fixture).querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  // forkJoin cancela la segunda petición cuando la primera falla; esa cancelación es esperada
  afterEach(() => controller.verify({ ignoreCancelled: true }));

  it('el Admin pide también los inactivos y ve los botones de edición', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    expect(text(fixture)).toContain('Jugo de frambuesa');
    expect(text(fixture)).toContain('$3.500');
    expect(text(fixture)).toContain('Nueva categoría');
    expect(text(fixture)).toContain('Nuevo producto');
  });

  it('un mesero solo lee: no pide inactivos ni ve botones de edición', async () => {
    const fixture = await loadAs('Waiter');

    expect(text(fixture)).toContain('Jugo de frambuesa');
    expect(text(fixture)).not.toContain('Nueva categoría');
    expect(text(fixture)).not.toContain('Editar');
    expect(text(fixture)).not.toContain('Desactivar');
  });

  it('filtra los productos por categoría', async () => {
    const fixture = await loadAs('Waiter');

    buttonByText(fixture, 'Postres').click();
    await fixture.whenStable();

    expect(text(fixture)).toContain('Helado artesanal');
    expect(text(fixture)).not.toContain('Jugo de frambuesa');
  });

  it('muestra un aviso con reintento si la carga falla', async () => {
    const fixture = create('Waiter');

    controller.expectOne('/api/categories').flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('No pudimos cargar el catálogo');
    expect(text(fixture)).toContain('Reintentar');
  });

  it('crea una categoría y vuelve a cargar la lista', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nueva categoría').click();
    await fixture.whenStable();
    type(fixture, '#category-name', 'Entradas');
    element(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));

    const post = controller.expectOne('/api/categories');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ name: 'Entradas', sortOrder: 2 });
    post.flush({ id: 'c3', name: 'Entradas', sortOrder: 2, isActive: true });

    // Después de guardar se recargan las dos listas
    controller.expectOne('/api/categories?includeInactive=true').flush([
      ...categories,
      { id: 'c3', name: 'Entradas', sortOrder: 2, isActive: true },
    ]);
    controller.expectOne('/api/products?includeInactive=true').flush(products);
    await fixture.whenStable();

    expect(text(fixture)).toContain('Entradas');
    expect(element(fixture).querySelector('form')).toBeNull();
  });

  it('muestra el error 409 de nombre duplicado dentro del formulario', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nueva categoría').click();
    await fixture.whenStable();
    type(fixture, '#category-name', 'bebidas');
    element(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));

    controller
      .expectOne('/api/categories')
      .flush({ detail: 'Ya existe una categoría con ese nombre.' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ya existe una categoría con ese nombre.');
    // El formulario sigue abierto para corregir
    expect(element(fixture).querySelector('form')).not.toBeNull();
  });

  it('no envía un producto sin nombre', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nuevo producto').click();
    await fixture.whenStable();
    element(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    controller.expectNone('/api/products');
    expect(text(fixture)).toContain('Escribe un nombre');
  });

  it('crea un producto con descripción vacía como null', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nuevo producto').click();
    await fixture.whenStable();
    type(fixture, '#product-name', 'Agua mineral');
    type(fixture, '#product-price', '1500');
    element(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));

    const post = controller.expectOne('/api/products');
    expect(post.request.body).toEqual({
      categoryId: 'c1',
      name: 'Agua mineral',
      description: null,
      price: 1500,
    });
    post.flush({ ...products[0], id: 'p3', name: 'Agua mineral', price: 1500 });

    controller.expectOne('/api/categories?includeInactive=true').flush(categories);
    controller.expectOne('/api/products?includeInactive=true').flush(products);
    await fixture.whenStable();
  });

  it('muestra el motivo si no se puede desactivar una categoría', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Bebidas').click();
    await fixture.whenStable();
    buttonByText(fixture, 'Desactivar categoría').click();

    const request = controller.expectOne('/api/categories/c1/deactivate');
    expect(request.request.method).toBe('POST');
    request.flush(
      { detail: 'No puedes desactivar una categoría que tiene productos activos.' },
      { status: 409, statusText: 'Conflict' },
    );
    await fixture.whenStable();

    expect(text(fixture)).toContain('No puedes desactivar una categoría que tiene productos activos.');
  });
});
