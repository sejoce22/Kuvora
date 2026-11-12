import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { OrdersService } from './orders.service';

describe('OrdersService', () => {
  let service: OrdersService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(OrdersService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('abre un pedido de salón con mesa y personas', () => {
    service.open({ channel: 'DineIn', tableId: 't1', guestCount: 3 }).subscribe();

    const request = controller.expectOne('/api/orders');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ channel: 'DineIn', tableId: 't1', guestCount: 3 });
    request.flush({});
  });

  it('pide los pedidos abiertos', () => {
    service.getOpen().subscribe();

    const request = controller.expectOne('/api/orders/open');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('pide el detalle de un pedido', () => {
    service.get('o1').subscribe();

    controller.expectOne('/api/orders/o1').flush({});
  });

  it('agrega un ítem', () => {
    service.addItem('o1', { productId: 'p1', quantity: 2, dinerNumber: 1 }).subscribe();

    const request = controller.expectOne('/api/orders/o1/items');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ productId: 'p1', quantity: 2, dinerNumber: 1 });
    request.flush({});
  });

  it('edita un ítem con PUT', () => {
    service.updateItem('o1', 'i1', { quantity: 3, note: 'sin sal', dinerNumber: null }).subscribe();

    const request = controller.expectOne('/api/orders/o1/items/i1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ quantity: 3, note: 'sin sal', dinerNumber: null });
    request.flush({});
  });

  it('quita un ítem con DELETE', () => {
    service.removeItem('o1', 'i1').subscribe();

    const request = controller.expectOne('/api/orders/o1/items/i1');
    expect(request.request.method).toBe('DELETE');
    request.flush({});
  });

  it('envía a cocina sin cuerpo', () => {
    service.sendToKitchen('o1').subscribe();

    const request = controller.expectOne('/api/orders/o1/send');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });

  it('cancela un ítem y un pedido con motivo', () => {
    service.cancelItem('o1', 'i1', { reason: 'Se cayó' }).subscribe();
    const item = controller.expectOne('/api/orders/o1/items/i1/cancel');
    expect(item.request.body).toEqual({ reason: 'Se cayó' });
    item.flush({});

    service.cancelOrder('o1', { reason: 'Error' }).subscribe();
    const order = controller.expectOne('/api/orders/o1/cancel');
    expect(order.request.body).toEqual({ reason: 'Error' });
    order.flush({});
  });
});
