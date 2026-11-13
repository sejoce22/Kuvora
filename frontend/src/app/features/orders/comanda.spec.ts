import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Comanda } from './comanda';
import { Order, OrderItem } from './orders.models';

describe('Comanda', () => {
  const item = (overrides: Partial<OrderItem>): OrderItem => ({
    id: 'i1',
    productId: 'p1',
    productName: 'Lomo saltado',
    unitPrice: 8_900,
    quantity: 2,
    subtotal: 17_800,
    note: null,
    dinerNumber: null,
    status: 'Sent',
    round: 1,
    sentAtUtc: '2026-11-12T20:30:00Z',
    cancelledAtUtc: null,
    cancelReason: null,
    ...overrides,
  });

  const order = (items: OrderItem[]): Order => ({
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
    total: 0,
    currentRound: 2,
    cancelledAtUtc: null,
    cancelReason: null,
    items,
  });

  let controller: HttpTestingController;

  function mount(round: string): ComponentFixture<Comanda> {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'o1', round }) } } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Comanda);
    fixture.detectChanges();
    return fixture;
  }

  const text = (fixture: ComponentFixture<Comanda>) => (fixture.nativeElement as HTMLElement).textContent ?? '';

  afterEach(() => controller.verify());

  it('imprime solo los ítems de la ronda pedida, con nota y comensal', async () => {
    const fixture = mount('1');
    controller.expectOne('/api/orders/o1').flush(
      order([
        item({ id: 'a', productName: 'Lomo saltado', note: 'Sin cebolla', dinerNumber: 2 }),
        item({ id: 'b', productName: 'Café', round: 2 }),
        item({ id: 'c', productName: 'Postre', round: null, status: 'Pending', sentAtUtc: null }),
      ]),
    );
    await fixture.whenStable();

    const content = text(fixture);
    expect(content).toContain('Comanda · Ronda 1');
    expect(content).toContain('#12');
    expect(content).toContain('Mesa 4');
    expect(content).toContain('Ana Pérez');
    expect(content).toContain('2 ×');
    expect(content).toContain('Lomo saltado');
    expect(content).toContain('Sin cebolla');
    expect(content).toContain('Comensal 2');
    expect(content).not.toContain('Café');
    expect(content).not.toContain('Postre');
  });

  it('marca como cancelado un ítem que se canceló después de enviarse', async () => {
    const fixture = mount('1');
    controller
      .expectOne('/api/orders/o1')
      .flush(order([item({ status: 'Cancelled', cancelReason: 'Se cayó' })]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('CANCELADO');
  });

  it('avisa si la ronda no existe', async () => {
    const fixture = mount('9');
    controller.expectOne('/api/orders/o1').flush(order([item({})]));
    await fixture.whenStable();

    expect(text(fixture)).toContain('Esta ronda no existe');
    expect(text(fixture)).toContain('Volver al pedido');
  });

  it('avisa si el pedido no existe', async () => {
    const fixture = mount('1');
    controller.expectOne('/api/orders/o1').flush(null, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('No encontramos este pedido');
  });
});
