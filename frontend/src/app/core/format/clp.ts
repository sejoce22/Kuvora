const formatter = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0,
});

/** Pesos chilenos sin decimales: 3500 → "$3.500". */
export function formatClp(amount: number): string {
  return formatter.format(amount);
}
