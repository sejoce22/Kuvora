import { formatElapsed } from './elapsed';

describe('formatElapsed', () => {
  const now = Date.parse('2026-11-12T20:00:00Z');
  const minutesAgo = (minutes: number) => new Date(now - minutes * 60_000).toISOString();

  it('menos de un minuto', () => {
    expect(formatElapsed(minutesAgo(0), now)).toBe('Recién abierto');
  });

  it('minutos', () => {
    expect(formatElapsed(minutesAgo(12), now)).toBe('12 min');
  });

  it('horas con minutos de dos dígitos', () => {
    expect(formatElapsed(minutesAgo(65), now)).toBe('1 h 05 min');
  });

  it('horas exactas', () => {
    expect(formatElapsed(minutesAgo(120), now)).toBe('2 h');
  });

  it('una fecha futura (reloj desfasado) no da tiempo negativo', () => {
    expect(formatElapsed(minutesAgo(-5), now)).toBe('Recién abierto');
  });
});
