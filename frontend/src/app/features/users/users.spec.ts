import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { User } from '../../core/auth/auth.models';
import { Users } from './users';

describe('Users', () => {
  const team: User[] = [
    { id: '1', tenantId: 't1', email: 'ana@kuvora.test', fullName: 'Ana Pérez', role: 'Admin' },
    { id: '2', tenantId: 't1', email: 'luis@kuvora.test', fullName: 'Luis Soto', role: 'Waiter' },
  ];

  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('muestra los usuarios del restaurante', async () => {
    const fixture = TestBed.createComponent(Users);
    fixture.detectChanges();

    controller.expectOne('/api/users').flush(team);
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Ana Pérez');
    expect(text).toContain('Mesero');
  });

  it('muestra un aviso si la carga falla', async () => {
    const fixture = TestBed.createComponent(Users);
    fixture.detectChanges();

    controller.expectOne('/api/users').flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('No pudimos cargar los usuarios');
  });
});
