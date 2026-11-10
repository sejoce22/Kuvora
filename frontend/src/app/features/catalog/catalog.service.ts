import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Category, Product, SaveCategoryRequest, SaveProductRequest } from './catalog.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);

  // Cualquier usuario autenticado puede leer. `includeInactive` solo lo respeta el backend para Admin.
  getCategories(includeInactive: boolean): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories', { params: this.params(includeInactive) });
  }

  getProducts(includeInactive: boolean): Observable<Product[]> {
    return this.http.get<Product[]>('/api/products', { params: this.params(includeInactive) });
  }

  // Escritura: solo Admin (el backend responde 403 al resto).
  createCategory(request: SaveCategoryRequest): Observable<Category> {
    return this.http.post<Category>('/api/categories', request);
  }

  updateCategory(id: string, request: SaveCategoryRequest): Observable<Category> {
    return this.http.put<Category>(`/api/categories/${id}`, request);
  }

  setCategoryActive(id: string, isActive: boolean): Observable<Category> {
    return this.http.post<Category>(`/api/categories/${id}/${isActive ? 'activate' : 'deactivate'}`, null);
  }

  createProduct(request: SaveProductRequest): Observable<Product> {
    return this.http.post<Product>('/api/products', request);
  }

  updateProduct(id: string, request: SaveProductRequest): Observable<Product> {
    return this.http.put<Product>(`/api/products/${id}`, request);
  }

  setProductActive(id: string, isActive: boolean): Observable<Product> {
    return this.http.post<Product>(`/api/products/${id}/${isActive ? 'activate' : 'deactivate'}`, null);
  }

  private params(includeInactive: boolean): HttpParams {
    return includeInactive ? new HttpParams().set('includeInactive', true) : new HttpParams();
  }
}
