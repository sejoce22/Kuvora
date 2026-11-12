// Límites iguales a los del backend (Domain): la pantalla avisa antes de que la API responda 400.
export const GUEST_COUNT_MIN = 1;
export const GUEST_COUNT_MAX = 50;
export const QUANTITY_MIN = 1;
export const QUANTITY_MAX = 99;
export const NOTE_MAX = 200;
export const DINER_MAX = 50;
export const REASON_MAX = 200;

// El backend envía los enums como texto.
export type OrderChannel = 'DineIn' | 'Counter';
export type OrderStatus = 'Open' | 'Cancelled';
export type OrderItemStatus = 'Pending' | 'Sent' | 'Cancelled';

export interface OrderItem {
  id: string;
  productId: string;
  // Copia del nombre y del precio al momento de la venta: no cambia si el producto cambia después.
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
  note: string | null;
  // null = ítem compartido entre todos los comensales
  dinerNumber: number | null;
  status: OrderItemStatus;
  // Número de la comanda en que se envió; null mientras está pendiente
  round: number | null;
  sentAtUtc: string | null;
  cancelledAtUtc: string | null;
  cancelReason: string | null;
}

// Detalle completo (pantalla de Pedido)
export interface Order {
  id: string;
  number: number;
  channel: OrderChannel;
  tableId: string | null;
  tableName: string | null;
  guestCount: number | null;
  waiterId: string;
  waiterName: string;
  status: OrderStatus;
  openedAtUtc: string;
  total: number;
  currentRound: number;
  cancelledAtUtc: string | null;
  cancelReason: string | null;
  items: OrderItem[];
}

// Resumen para la sala: alimenta el estado de las mesas
export interface OrderSummary {
  id: string;
  number: number;
  channel: OrderChannel;
  tableId: string | null;
  tableName: string | null;
  waiterName: string;
  guestCount: number | null;
  itemCount: number;
  total: number;
  openedAtUtc: string;
}

export interface OpenOrderRequest {
  channel: OrderChannel;
  tableId?: string;
  guestCount?: number;
}

export interface AddOrderItemRequest {
  productId: string;
  quantity: number;
  note?: string | null;
  dinerNumber?: number | null;
}

export interface UpdateOrderItemRequest {
  quantity: number;
  note: string | null;
  dinerNumber: number | null;
}

export interface CancelRequest {
  reason: string;
}
