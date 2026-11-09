import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { Session } from './auth.models';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  const session: Session = {
    accessToken: 'token-123',
    expiresAtUtc: '2030-01-01T00:00:00Z',
    user: {
      id: '1',
      tenantId: 't1',
      email: 'ana@kuvora.test',
      fullName: 'Ana Pérez',
      role: 'Admin',
    },
  };

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => controller.verify());

  function signIn(): void {
    auth.login({ email: 'ana@kuvora.test', password: 'x' }).subscribe();
    controller.expectOne('/api/auth/login').flush(session);
  }

  it('agrega el token a las peticiones a la API', () => {
    signIn();

    http.get('/api/users').subscribe();

    const req = controller.expectOne('/api/users');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-123');
    req.flush([]);
  });

  it('no agrega cabecera si no hay sesión', () => {
    http.get('/api/users').subscribe();

    const req = controller.expectOne('/api/users');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('no agrega el token a las rutas públicas de autenticación', () => {
    signIn();

    http.post('/api/auth/refresh', null).subscribe();

    const req = controller.expectOne('/api/auth/refresh');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(session);
  });

  it('ante un 401 renueva la sesión y reintenta la petición', () => {
    signIn();
    let result: unknown;
    http.get('/api/users').subscribe((r) => (result = r));

    controller.expectOne('/api/users').flush(null, unauthorized);
    controller.expectOne('/api/auth/refresh').flush({ ...session, accessToken: 'token-nuevo' });

    const retry = controller.expectOne('/api/users');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-nuevo');
    retry.flush(['ok']);
    expect(result).toEqual(['ok']);
  });

  it('con varias peticiones fallando a la vez, renueva una sola vez', () => {
    signIn();
    http.get('/api/users').subscribe();
    http.get('/api/tables').subscribe();

    controller.expectOne('/api/users').flush(null, unauthorized);
    controller.expectOne('/api/tables').flush(null, unauthorized);

    // expectOne falla si hubiera dos peticiones de renovación
    controller.expectOne('/api/auth/refresh').flush({ ...session, accessToken: 'token-nuevo' });
    controller.expectOne('/api/users').flush([]);
    controller.expectOne('/api/tables').flush([]);
  });

  it('si la renovación falla, cierra la sesión y manda al login', () => {
    signIn();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    let status: number | undefined;
    http.get('/api/users').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });

    controller.expectOne('/api/users').flush(null, unauthorized);
    controller.expectOne('/api/auth/refresh').flush(null, unauthorized);

    expect(status).toBe(401);
    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('no renueva ante errores distintos de 401', () => {
    signIn();
    http.get('/api/users').subscribe({ error: () => undefined });

    controller.expectOne('/api/users').flush(null, { status: 500, statusText: 'Server Error' });

    controller.expectNone('/api/auth/refresh');
  });

  it('un 401 en el login no intenta renovar', () => {
    auth.login({ email: 'ana@kuvora.test', password: 'mala' }).subscribe({ error: () => undefined });

    controller.expectOne('/api/auth/login').flush(null, unauthorized);

    controller.expectNone('/api/auth/refresh');
  });
});
