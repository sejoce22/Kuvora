import { HttpErrorResponse } from '@angular/common/http';
import { problemMessage } from './problem-details';

describe('problemMessage', () => {
  const fallback = 'Algo salió mal.';

  it('usa el detalle del ProblemDetails cuando existe', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { detail: 'Ya existe una categoría con ese nombre.' },
    });

    expect(problemMessage(error, fallback)).toBe('Ya existe una categoría con ese nombre.');
  });

  it('explica el 403 sin depender del cuerpo', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 403 }), fallback)).toContain('permiso');
  });

  it('avisa de problemas de conexión (status 0)', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 0 }), fallback)).toContain('conectar');
  });

  it('pide revisar el formulario ante un 400 sin detalle', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 400, error: null }), fallback)).toContain(
      'Revisa',
    );
  });

  it('usa el mensaje de respaldo en cualquier otro caso', () => {
    expect(problemMessage(new HttpErrorResponse({ status: 500, error: null }), fallback)).toBe(fallback);
  });
});
