import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login/login').then((m) => m.Login) },
  { path: '', pathMatch: 'full', loadComponent: () => import('./features/home/home').then((m) => m.Home) },
  { path: '**', redirectTo: '' },
];
