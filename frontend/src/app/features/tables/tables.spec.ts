import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Role, User } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { Tables } from './tables';
import { DiningTable } from './tables.models';

describe('Tables', () => {
  // En el orden en que las entrega el backend (por zona y luego por nombre como texto: "Mesa 10" antes que "Mesa 2")
  const tables: DiningTable[] = [
    { id: 't1', name: 'Mesa 1', capacity: 1, zone: 'Terraza', isActive: true },
    { id: 't10', name: 'Mesa 10', capacity: 4, zone: 'Terraza', isActive: true },
    { id: 't2', name: 'Mesa 2', capacity: 2, zone: 'Terraza', isActive: true },
    { id: 'b1', name: 'Barra 1', capacity: 2, zone: null, isActive: true },
  ];

  let controller: HttpTestingController;

  function create(role: Role): ComponentFixture<Tables> {
    const user: User = { id: 'u1', tenantId: 't1', email: 'ana@kuvora.test', fullName: 'Ana Pérez', role };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { user: signal(user) } },
      ],
    });
    controller = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Tables);
    fixture.detectChanges();
    return fixture;
  }

  async function loadAs(role: Role, query = ''): Promise<ComponentFixture<Tables>> {
    const fixture = create(role);
    controller.expectOne(`/api/tables${query}`).flush(tables);
    await fixture.whenStable();
    return fixture;
  }

  const element = (fixture: ComponentFixture<Tables>) => fixture.nativeElement as HTMLElement;
  const text = (fixture: ComponentFixture<Tables>) => element(fixture).textContent ?? '';

  function buttonByText(fixture: ComponentFixture<Tables>, label: string): HTMLButtonElement {
    const button = Array.from(element(fixture).querySelectorAll('button')).find((b) =>
      b.textContent?.includes(label),
    );
    if (!button) throw new Error(`No hay un botón con el texto "${label}"`);
    return button;
  }

  function type(fixture: ComponentFixture<Tables>, selector: string, value: string): void {
    const input = element(fixture).querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  const submit = (fixture: ComponentFixture<Tables>) =>
    element(fixture).querySelector('form')!.dispatchEvent(new Event('submit'));

  afterEach(() => controller.verify());

  it('el Admin pide también las inactivas y ve los botones de edición', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    expect(text(fixture)).toContain('Mesa 1');
    expect(text(fixture)).toContain('Nueva mesa');
    expect(text(fixture)).toContain('Editar');
  });

  it('un mesero solo lee: no pide inactivas ni ve botones de edición', async () => {
    const fixture = await loadAs('Waiter');

    expect(text(fixture)).toContain('Mesa 1');
    expect(text(fixture)).not.toContain('Nueva mesa');
    expect(text(fixture)).not.toContain('Editar');
    expect(text(fixture)).not.toContain('Desactivar');
  });

  it('agrupa por zona y deja "Sin zona" al final', async () => {
    const fixture = await loadAs('Waiter');

    const content = text(fixture);
    expect(content).toContain('Terraza');
    expect(content.indexOf('Terraza')).toBeLessThan(content.indexOf('Sin zona'));
    expect(content.indexOf('Sin zona')).toBeLessThan(content.indexOf('Barra 1'));
  });

  it('ordena los nombres de forma natural: Mesa 2 antes que Mesa 10', async () => {
    const fixture = await loadAs('Waiter');

    const content = text(fixture);
    expect(content.indexOf('Mesa 2')).toBeLessThan(content.indexOf('Mesa 10'));
  });

  it('usa el singular para una sola persona', async () => {
    const fixture = await loadAs('Waiter');

    expect(text(fixture)).toContain('1 persona');
    expect(text(fixture)).toContain('4 personas');
  });

  it('muestra un aviso con reintento si la carga falla', async () => {
    const fixture = create('Waiter');

    controller.expectOne('/api/tables').flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('No pudimos cargar las mesas');
    expect(text(fixture)).toContain('Reintentar');
  });

  it('crea una mesa sin zona como null y vuelve a cargar la lista', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nueva mesa').click();
    await fixture.whenStable();
    type(fixture, '#table-name', 'Mesa 5');
    submit(fixture);

    const post = controller.expectOne('/api/tables');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ name: 'Mesa 5', capacity: 4, zone: null });
    post.flush({ id: 't5', name: 'Mesa 5', capacity: 4, zone: null, isActive: true });

    controller.expectOne('/api/tables?includeInactive=true').flush(tables);
    await fixture.whenStable();

    expect(element(fixture).querySelector('form')).toBeNull();
  });

  it('muestra el error 409 de nombre duplicado dentro del formulario', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nueva mesa').click();
    await fixture.whenStable();
    type(fixture, '#table-name', 'mesa 1');
    submit(fixture);

    controller
      .expectOne('/api/tables')
      .flush({ detail: 'Ya existe una mesa con ese nombre.' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(text(fixture)).toContain('Ya existe una mesa con ese nombre.');
    expect(element(fixture).querySelector('form')).not.toBeNull();
  });

  it('no envía una capacidad fuera de rango', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Nueva mesa').click();
    await fixture.whenStable();
    type(fixture, '#table-name', 'Mesa 6');
    type(fixture, '#table-capacity', '0');
    submit(fixture);
    await fixture.whenStable();

    controller.expectNone('/api/tables');
    expect(text(fixture)).toContain('entre 1 y 50');
  });

  it('desactiva una mesa y vuelve a cargar la lista', async () => {
    const fixture = await loadAs('Admin', '?includeInactive=true');

    buttonByText(fixture, 'Desactivar').click();

    const request = controller.expectOne('/api/tables/t1/deactivate');
    expect(request.request.method).toBe('POST');
    request.flush({ ...tables[0], isActive: false });

    controller.expectOne('/api/tables?includeInactive=true').flush(tables);
    await fixture.whenStable();
  });
});
