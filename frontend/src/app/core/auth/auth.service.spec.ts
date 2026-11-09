import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Session } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const credentials = { email: 'ana@madero.test', password: 'Prueba-12345' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify()); // falla si quedó alguna petición sin responder

  it('starts without a session', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('stores the session in memory after a successful login', () => {
    const session: Session = {
      accessToken: 'token-de-prueba',
      expiresAtUtc: '2026-11-08T12:00:00Z',
      user: {
        id: '1',
        tenantId: '2',
        email: credentials.email,
        fullName: 'Ana Pérez',
        role: 'Admin',
      },
    };

    service.login(credentials).subscribe();

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(credentials);
    request.flush(session);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.fullName).toBe('Ana Pérez');
    expect(service.accessToken()).toBe('token-de-prueba');
  });

  it('stays signed out when the credentials are wrong', () => {
    let status = 0;
    service.login(credentials).subscribe({
      error: (error: HttpErrorResponse) => (status = error.status),
    });

    http.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(service.isAuthenticated()).toBe(false);
  });
});
