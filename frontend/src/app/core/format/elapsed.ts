/** Tiempo transcurrido desde una fecha ISO, en texto corto: "Recién abierto", "12 min", "1 h 05 min". */
export function formatElapsed(fromIso: string, nowMs: number): string {
  const minutes = Math.max(0, Math.floor((nowMs - Date.parse(fromIso)) / 60_000));

  if (minutes < 1) return 'Recién abierto';
  if (minutes < 60) return `${minutes} min`;

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return rest === 0 ? `${hours} h` : `${hours} h ${rest.toString().padStart(2, '0')} min`;
}
