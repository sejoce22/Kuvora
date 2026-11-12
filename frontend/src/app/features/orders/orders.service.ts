import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AddOrderItemRequest,
  CancelRequest,
  OpenOrderRequest,
  Order,
  OrderSummary,
  UpdateOrderItemRequest,
} from './orders.models';

// Admin, Mesero y Cajero (política ManageOrders). Cocina recibe 403.
// Las operaciones sobre ítems devuelven el pedido completo: la pantalla actualiza total y rondas con eso.
@Injectable({ providedIn: 'root' })
export class OrdersService {
  private readonly http = inject(HttpClient);

  open(request: OpenOrderRequest): Observable<Order> {
    return this.http.post<Order>('/api/orders', request);
  }

  getOpen(): Observable<OrderSummary[]> {
    return this.http.get<OrderSummary[]>('/api/orders/open');
  }

  get(id: string): Observable<Order> {
    return this.http.get<Order>(`/api/orders/${id}`);
  }

  addItem(orderId: string, request: AddOrderItemRequest): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${orderId}/items`, request);
  }

  updateItem(orderId: string, itemId: string, request: UpdateOrderItemRequest): Observable<Order> {
    return this.http.put<Order>(`/api/orders/${orderId}/items/${itemId}`, request);
  }

  removeItem(orderId: string, itemId: string): Observable<Order> {
    return this.http.delete<Order>(`/api/orders/${orderId}/items/${itemId}`);
  }

  // Idempotente en la práctica: sin ítems pendientes el backend responde 409 y no duplica la comanda.
  sendToKitchen(orderId: string): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${orderId}/send`, null);
  }

  // Solo Admin (403 para el resto)
  cancelItem(orderId: string, itemId: string, request: CancelRequest): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${orderId}/items/${itemId}/cancel`, request);
  }

  // El mesero puede si nada se envió; con ítems enviados, solo Admin.
  cancelOrder(orderId: string, request: CancelRequest): Observable<Order> {
    return this.http.post<Order>(`/api/orders/${orderId}/cancel`, request);
  }
}
