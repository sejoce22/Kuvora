import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-home',
  template: `
    <main>
      <h1>Hola, {{ auth.user()?.fullName }}</h1>
      <p>Rol: {{ auth.user()?.role }}</p>
    </main>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
}
