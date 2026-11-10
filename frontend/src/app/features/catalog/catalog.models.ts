// Límites iguales a los del backend (Domain): la pantalla avisa antes de que la API responda 400.
export const CATEGORY_NAME_MAX = 60;
export const PRODUCT_NAME_MAX = 100;
export const PRODUCT_DESCRIPTION_MAX = 300;
export const PRODUCT_PRICE_MAX = 100_000_000;

export interface Category {
  id: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
}

export interface Product {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  // Pesos chilenos enteros
  price: number;
  isActive: boolean;
}

export interface SaveCategoryRequest {
  name: string;
  sortOrder: number;
}

export interface SaveProductRequest {
  categoryId: string;
  name: string;
  description: string | null;
  price: number;
}
