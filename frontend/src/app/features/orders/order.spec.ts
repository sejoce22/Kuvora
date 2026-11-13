import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Role, User } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { Category, Product } from '../catalog/catalog.models';
import { OrderScreen } from './order';
import { Order, OrderItem } from './orders.models';

describe('OrderScreen', () => {
  const categories: Category[] = [
    { id: 'c1', name: 'Platos', sortOrder: 1, isActive: true },
    { id: 'c2', name: 'Bebidas', sortOrder: 2, isActive: true },
  ];

  const products: Product[] = [
    { id: 'p1', categoryId: 'c1', categoryName: 'Platos', name: 'Lomo saltado', description: null, price: 8_900, isActive: true },
    { id: 'p2', categoryId: 'c2', categoryName: 'Bebidas', name: 'Café', description: null, price: 2_500, isActive: true },
  ];

  const item = (overrides: Partial<OrderItem>): OrderItem => ({
    id: 'i1',
    productId: 'p1',
    productName: 'Lomo saltado',
    unitPrice: 8_900,
    quantity: 1,
    subtotal: 8_900,
    note: null,
    dinerNumber: null,
    status: 'Pending',
    round: null,
    sentAtUtc: null,
    cancelledAtUtc: null,
    cancelReason: null,
    ...overrides,
  });

  const order = (items: OrderItem[] = [], overrides: Partial<Order> = {}): Order => ({
    id: 'o1',
    number: 12,
    channel: 'DineIn',
    tableId: 't4',
    tableName: 'Mesa 4',
    guestCount: 3,
    waiterId: 'u1',
    waiterName: 'Ana Pérez',
    status: 'Open',
    openedAtUtc: '2026-11-12T20:00:00Z',
    total: items.filter((i) => i.status !== 'Cancelled').reduce((sum, i) => sum + i.subtotal, 0),
    currentRound: Math.max(0, ...items.map((i) => i.round ?? 0)),
    cancelledAtUtc: null,
    cancelReason: null,
    items,
    ...overrides,
  });

  let controller: HttpTestingController;
  let navigated: unknown[][];

  // Monta la pantalla con un Router de verdad (el enlace "Mesas" lo necesita) y espía las navegaciones.
  function mount(role: Role): ComponentFixture<OrderScreen> {
    const user: User = { id: 'u1', tenantId: 't1', email: 'ana@kuvora.test', fullName: 'Ana Pérez', role };
    navigated = [];

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { user: signal(user) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'o1' }) } } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);

    TestBed.inject(Router).navigate = (commands: readonly unknown[]) => {
      navigated.push([...commands]);
      return Promise.resolve(true);
    };

    const fixture = TestBed.createComponent(OrderScreen);
    fixture.detectChanges();
    return fixture;
  }

  async function open(role: Role, initial: Order): Promise<ComponentFixture<OrderScreen>> {
    const fixture = mount(role);

    controller.expectOne('/api/orders/o1').flush(initial);
    controller.expectOne('/api/categories').flush(categories);
    controller.expectOne('/api/products').flush(products);
    await fixture.whenStable();
    return fixture;
  }

  const element = (fixture: ComponentFixture<OrderScreen>) => fixture.nativeElement as HTMLElement;
  const text = (fixture: ComponentFixture<OrderScreen>) => element(fixture).textContent ?? '';

  function button(fixture: ComponentFixture<OrderScreen>, label: string): HTMLButtonElement {
    const found = Array.from(element(fixture).querySelectorAll('button')).find((b) => b.textContent?.includes(label));
    if (!found) throw new Error(`No hay un botón con el texto "${label}"`);
    return found;
  }

  function type(fixture: ComponentFixture<OrderScreen>, selector: string, value: string): HTMLInputElement {
    const input = element(fixture).querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    return input;
  }

  afterEach(() => controller.verify());

  it('muestra el encabezado, los ítems por enviar y el total', async () => {
    const fixture = await open('Waiter', order([item({ quantity: 2, subtotal: 17_800 })]));

    expect(text(fixture)).toContain('Mesa 4');
    expect(text(fixture)).toContain('Pedido #12');
    expect(text(fixture)).toContain('3 personas');
    expect(text(fixture)).toContain('Lomo saltado');
    expect(text(fixture)).toContain('17.800');
  });

  it('un toque en un producto lo agrega a la comanda', async () => {
    const fixture = await open('Waiter', order());

    button(fixture, 'Café').click();

    const post = controller.expectOne('/api/orders/o1/items');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ productId: 'p2', quantity: 1, dinerNumber: null });
    post.flush(order([item({ id: 'i9', productId: 'p2', productName: 'Café', unitPrice: 2_500, subtotal: 2_500 })]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('2.500');
  });

  it('repetir el mismo producto suma a la línea pendiente en vez de crear otra', async () => {
    const fixture = await open('Waiter', order([item({ id: 'i1', productId: 'p1' })]));

    button(fixture, 'Lomo saltado').click();

    const put = controller.expectOne('/api/orders/o1/items/i1');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ quantity: 2, note: null, dinerNumber: null });
    put.flush(order([item({ quantity: 2, subtotal: 17_800 })]));
  });

  it('el comensal elegido se envía junto con el producto', async () => {
    const fixture = await open('Waiter', order());

    button(fixture, 'Comensal 2').click();
    button(fixture, 'Café').click();

    const post = controller.expectOne('/api/orders/o1/items');
    expect(post.request.body).toEqual({ productId: 'p2', quantity: 1, dinerNumber: 2 });
    post.flush(order());
  });

  it('quitar una línea avisa con Deshacer, y Deshacer la vuelve a agregar', async () => {
    const fixture = await open('Waiter', order([item({ quantity: 2, subtotal: 17_800, note: 'sin sal' })]));

    button(fixture, 'Quitar').click();
    const del = controller.expectOne('/api/orders/o1/items/i1');
    expect(del.request.method).toBe('DELETE');
    del.flush(order());
    await fixture.whenStable();

    expect(text(fixture)).toContain('Quitaste Lomo saltado');

    button(fixture, 'Deshacer').click();
    const post = controller.expectOne('/api/orders/o1/items');
    expect(post.request.body).toEqual({ productId: 'p1', quantity: 2, note: 'sin sal', dinerNumber: null });
    post.flush(order([item({ id: 'i2', quantity: 2, subtotal: 17_800, note: 'sin sal' })]));
  });

  it('una nota rápida se agrega con un toque', async () => {
    const fixture = await open('Waiter', order([item({})]));

    button(fixture, '+ Nota').click();
    await fixture.whenStable();
    button(fixture, 'Sin cebolla').click();

    const put = controller.expectOne('/api/orders/o1/items/i1');
    expect(put.request.body).toEqual({ quantity: 1, note: 'Sin cebolla', dinerNumber: null });
    put.flush(order([item({ note: 'Sin cebolla' })]));
  });

  it('enviar a cocina crea la ronda y la muestra', async () => {
    const fixture = await open('Waiter', order([item({})]));

    button(fixture, 'Enviar a cocina').click();

    const post = controller.expectOne('/api/orders/o1/send');
    expect(post.request.method).toBe('POST');
    post.flush(order([item({ status: 'Sent', round: 1, sentAtUtc: '2026-11-12T20:30:00Z' })]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ronda 1');
    expect(text(fixture)).toContain('Ronda 1 enviada a cocina');
  });

  it('Ctrl+Enter envía a cocina y no hace nada si no hay ítems pendientes', async () => {
    const fixture = await open('Waiter', order([item({})]));

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true }));
    controller
      .expectOne('/api/orders/o1/send')
      .flush(order([item({ status: 'Sent', round: 1, sentAtUtc: '2026-11-12T20:30:00Z' })]));
    await fixture.whenStable();

    // Ahora no queda nada pendiente: el atajo no pide nada al backend
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true }));
    controller.expectNone('/api/orders/o1/send');
  });

  it('buscar filtra la carta sin importar tildes, y Enter agrega el primer resultado', async () => {
    const fixture = await open('Waiter', order());

    const input = type(fixture, '#order-search', 'cafe');
    await fixture.whenStable();

    expect(text(fixture)).toContain('Café');
    expect(text(fixture)).not.toContain('Lomo saltado');

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    const post = controller.expectOne('/api/orders/o1/items');
    expect(post.request.body).toEqual({ productId: 'p2', quantity: 1, dinerNumber: null });
    post.flush(order());
  });

  it('el mesero no ve "Cancelar ítem" y, con ítems enviados, tampoco "Cancelar pedido"', async () => {
    const sent = item({ status: 'Sent', round: 1, sentAtUtc: '2026-11-12T20:30:00Z' });
    const fixture = await open('Waiter', order([sent]));

    expect(text(fixture)).not.toContain('Cancelar ítem');
    expect(text(fixture)).not.toContain('Cancelar pedido');
  });

  it('el mesero puede cancelar un pedido que no salió a cocina', async () => {
    const fixture = await open('Waiter', order([item({})]));

    expect(text(fixture)).toContain('Cancelar pedido');
  });

  it('el Admin cancela un ítem enviado con motivo', async () => {
    const sent = item({ status: 'Sent', round: 1, sentAtUtc: '2026-11-12T20:30:00Z' });
    const fixture = await open('Admin', order([sent]));

    button(fixture, 'Cancelar ítem').click();
    await fixture.whenStable();
    type(fixture, '#cancel-reason', 'Se cayó el plato');
    element(fixture).querySelector('form.cancel')!.dispatchEvent(new Event('submit'));

    const post = controller.expectOne('/api/orders/o1/items/i1/cancel');
    expect(post.request.body).toEqual({ reason: 'Se cayó el plato' });
    post.flush(order([{ ...sent, status: 'Cancelled', cancelReason: 'Se cayó el plato' }]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Cancelados');
  });

  it('cancelar el pedido exige un motivo y luego vuelve a las mesas', async () => {
    const fixture = await open('Waiter', order([item({})]));

    button(fixture, 'Cancelar pedido').click();
    await fixture.whenStable();
    element(fixture).querySelector('form.cancel')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    controller.expectNone('/api/orders/o1/cancel');
    expect(text(fixture)).toContain('Escribe el motivo');

    type(fixture, '#cancel-reason', 'Abierto por error');
    element(fixture).querySelector('form.cancel')!.dispatchEvent(new Event('submit'));

    const post = controller.expectOne('/api/orders/o1/cancel');
    expect(post.request.body).toEqual({ reason: 'Abierto por error' });
    post.flush(order([], { status: 'Cancelled', cancelReason: 'Abierto por error' }));

    expect(navigated).toEqual([['/mesas']]);
  });

  it('un pedido cancelado no deja agregar productos', async () => {
    const fixture = await open('Waiter', order([], { status: 'Cancelled', cancelReason: 'Error' }));

    expect(text(fixture)).toContain('Pedido cancelado');
    expect(button(fixture, 'Café').disabled).toBe(true);
    expect(text(fixture)).not.toContain('Enviar a cocina');
  });

  it('un pedido inexistente muestra un aviso con el camino de vuelta', async () => {
    const fixture = mount('Waiter');

    controller.expectOne('/api/orders/o1').flush(null, { status: 404, statusText: 'Not Found' });
    controller.expectOne('/api/categories').flush(categories);
    controller.expectOne('/api/products').flush(products);
    await fixture.whenStable();

    expect(text(fixture)).toContain('No encontramos este pedido');
    expect(text(fixture)).toContain('Volver a las mesas');
  });
});
