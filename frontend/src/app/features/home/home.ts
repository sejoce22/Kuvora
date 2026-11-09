import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly firstName = computed(() => this.auth.user()?.fullName.split(' ')[0] ?? '');

  // Módulos que irán llegando en las próximas fases (solo decorativos por ahora)
  protected readonly upcoming = ['Mesas', 'Comandas', 'Cocina', 'Caja'];

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
