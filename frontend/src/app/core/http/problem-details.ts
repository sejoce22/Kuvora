import { HttpErrorResponse } from '@angular/common/http';

/**
 * Convierte un error HTTP en un mensaje para mostrar al usuario.
 * La API responde con ProblemDetails; su campo `detail` solo trae texto nuestro (409 y 404),
 * así que es seguro mostrarlo tal cual. Para lo demás usamos mensajes genéricos.
 */
export function problemMessage(error: HttpErrorResponse, fallback: string): string {
  if (error.status === 0) {
    return 'No pudimos conectar con el servidor. Inténtalo de nuevo en unos minutos.';
  }

  if (error.status === 403) {
    return 'No tienes permiso para hacer esto.';
  }

  const detail = (error.error as { detail?: unknown } | null)?.detail;
  if (typeof detail === 'string' && detail.length > 0) {
    return detail;
  }

  if (error.status === 400) {
    return 'Revisa los datos del formulario.';
  }

  return fallback;
}
