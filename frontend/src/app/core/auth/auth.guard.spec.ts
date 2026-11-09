import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { Role } from './auth.models';
import { authGuard, guestGuard, roleGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('guards', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  function signInAs(role: Role): void {
    TestBed.inject(AuthService)
      .login({ email: 'ana@kuvora.test', password: 'x' })
      .subscribe();
    controller.expectOne('/api/auth/login').flush({
      accessToken: 'token-123',
      expiresAtUtc: '2030-01-01T00:00:00Z',
      user: { id: '1', tenantId: 't1', email: 'ana@kuvora.test', fullName: 'Ana Pérez', role },
    });
  }

  function run(guard: CanActivateFn): unknown {
    return TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  function redirectTo(result: unknown): string {
    expect(result).toBeInstanceOf(UrlTree);
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  it('authGuard deja pasar con sesión', () => {
    signInAs('Admin');
    expect(run(authGuard)).toBe(true);
  });

  it('authGuard manda a /login sin sesión', () => {
    expect(redirectTo(run(authGuard))).toBe('/login');
  });

  it('guestGuard deja pasar sin sesión', () => {
    expect(run(guestGuard)).toBe(true);
  });

  it('guestGuard manda al inicio si ya hay sesión', () => {
    signInAs('Admin');
    expect(redirectTo(run(guestGuard))).toBe('/');
  });

  it('roleGuard deja pasar si el rol está permitido', () => {
    signInAs('Admin');
    expect(run(roleGuard('Admin'))).toBe(true);
  });

  it('roleGuard manda al inicio si el rol no está permitido', () => {
    signInAs('Waiter');
    expect(redirectTo(run(roleGuard('Admin')))).toBe('/');
  });

  it('roleGuard manda al inicio si no hay sesión', () => {
    expect(redirectTo(run(roleGuard('Admin')))).toBe('/');
  });
});
