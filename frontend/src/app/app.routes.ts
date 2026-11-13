import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    // Layout con barra superior; todo lo de adentro exige sesión.
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        // Cualquier usuario autenticado puede ver el catálogo; solo el Admin ve los botones de edición.
        path: 'catalogo',
        loadComponent: () => import('./features/catalog/catalog').then((m) => m.Catalog),
      },
      {
        // Cualquier usuario autenticado puede ver las mesas; solo el Admin ve los botones de edición.
        path: 'mesas',
        loadComponent: () => import('./features/tables/tables').then((m) => m.Tables),
      },
      {
        // Pantalla de pedido; el backend decide quién puede operar (Admin, Mesero y Cajero).
        path: 'pedidos/:id',
        loadComponent: () => import('./features/orders/order').then((m) => m.OrderScreen),
      },
      {
        // Comanda imprimible de una ronda (80 mm)
        path: 'pedidos/:id/comanda/:round',
        loadComponent: () => import('./features/orders/comanda').then((m) => m.Comanda),
      },
      {
        path: 'usuarios',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/users/users').then((m) => m.Users),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
