import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { User } from '../../core/auth/auth.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  // Solo Admin: el backend responde 403 al resto. El token lo agrega el interceptor.
  getAll(): Observable<User[]> {
    return this.http.get<User[]>('/api/users');
  }
}
