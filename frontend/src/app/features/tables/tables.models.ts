// Límites iguales a los del backend (Domain): la pantalla avisa antes de que la API responda 400.
export const TABLE_NAME_MAX = 40;
export const TABLE_ZONE_MAX = 40;
export const TABLE_MIN_CAPACITY = 1;
export const TABLE_MAX_CAPACITY = 50;

export interface DiningTable {
  id: string;
  name: string;
  capacity: number;
  // Texto libre (Terraza, Salón…); null = sin zona
  zone: string | null;
  isActive: boolean;
}

export interface SaveDiningTableRequest {
  name: string;
  capacity: number;
  zone: string | null;
}

// Mesas de una misma zona, ya ordenadas para mostrarlas
export interface ZoneGroup {
  zone: string | null;
  tables: DiningTable[];
}
