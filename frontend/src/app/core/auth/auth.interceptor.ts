import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Rutas públicas de autenticación: nunca llevan el token ni disparan renovación.
const PUBLIC_AUTH_URLS = [
  '/api/auth/login',
  '/api/auth/register',
  '/api/auth/refresh',
  '/api/auth/logout',
];

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  // Las peticiones son inmutables: se clona con la cabecera nueva.
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isApiCall = req.url.startsWith('/api/');
  const isPublic = PUBLIC_AUTH_URLS.some((url) => req.url.startsWith(url));
  if (!isApiCall || isPublic) {
    return next(req);
  }

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return auth.refreshShared().pipe(
        catchError((refreshError: unknown) => {
          void router.navigateByUrl('/login');
          return throwError(() => refreshError);
        }),
        // Este switchMap va después del catchError: un fallo del reintento no manda al login.
        switchMap((session) => next(withToken(req, session.accessToken))),
      );
    }),
  );
};
