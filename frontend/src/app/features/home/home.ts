import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home',
  template: `
    <main>
      <h1>Hola, {{ auth.user()?.fullName }}</h1>
      <p>Rol: {{ auth.user()?.role }}</p>
      <button type="button" (click)="logout()">Cerrar sesión</button>
    </main>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
