export type Role = 'Admin' | 'Waiter' | 'Kitchen' | 'Cashier';

export interface User {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  role: Role;
}

export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

// Nombres para mostrar en pantalla; el valor interno (Role) viene tal cual del backend.
export const ROLE_LABELS: Record<Role, string> = {
  Admin: 'Administrador',
  Waiter: 'Mesero',
  Kitchen: 'Cocina',
  Cashier: 'Cajero',
};
