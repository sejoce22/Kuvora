import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

// Solo deja pasar a usuarios con sesión; al resto los manda a /login.
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

// Para /login: si ya hay sesión no tiene sentido mostrar el formulario.
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
};

// Deja pasar solo a los roles indicados; al resto los manda al inicio.
// Es una fábrica: roleGuard('Admin') devuelve el guard configurado para esa ruta.
export const roleGuard =
  (...roles: Role[]): CanActivateFn =>
  () => {
    const role = inject(AuthService).user()?.role;
    return role !== undefined && roles.includes(role) ? true : inject(Router).createUrlTree(['/']);
  };
