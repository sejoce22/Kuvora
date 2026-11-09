import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { LoginRequest, Session } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  // El access token vive solo en memoria: nunca en localStorage.
  private readonly session = signal<Session | null>(null);

  readonly user = computed(() => this.session()?.user ?? null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  login(request: LoginRequest): Observable<Session> {
    return this.http
      .post<Session>('/api/auth/login', request)
      .pipe(tap((session) => this.session.set(session)));
  }

    refresh(): Observable<Session> {
    return this.http
      .post<Session>('/api/auth/refresh', null)
      .pipe(tap((session) => this.session.set(session)));
  }

  // Al arrancar la app: intenta recuperar la sesión con la cookie. Nunca falla;
  // si no hay sesión válida simplemente devuelve false.
  restoreSession(): Observable<boolean> {
    return this.refresh().pipe(
      map(() => true),
      catchError(() => of(false)),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null).pipe(
      catchError(() => of(undefined)), // aunque falle la red, cerramos la sesión local
      tap(() => this.session.set(null)),
    );
  }
}
