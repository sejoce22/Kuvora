import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { DiningTable, SaveDiningTableRequest } from './tables.models';

@Injectable({ providedIn: 'root' })
export class TablesService {
  private readonly http = inject(HttpClient);

  // Cualquier usuario autenticado puede leer. `includeInactive` solo lo respeta el backend para Admin.
  getAll(includeInactive: boolean): Observable<DiningTable[]> {
    const params = includeInactive ? new HttpParams().set('includeInactive', true) : new HttpParams();
    return this.http.get<DiningTable[]>('/api/tables', { params });
  }

  // Escritura: solo Admin (el backend responde 403 al resto).
  create(request: SaveDiningTableRequest): Observable<DiningTable> {
    return this.http.post<DiningTable>('/api/tables', request);
  }

  update(id: string, request: SaveDiningTableRequest): Observable<DiningTable> {
    return this.http.put<DiningTable>(`/api/tables/${id}`, request);
  }

  setActive(id: string, isActive: boolean): Observable<DiningTable> {
    return this.http.post<DiningTable>(`/api/tables/${id}/${isActive ? 'activate' : 'deactivate'}`, null);
  }
}
