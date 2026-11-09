import { Component, OnInit, inject, signal } from '@angular/core';
import { ROLE_LABELS, User } from '../../core/auth/auth.models';
import { UsersService } from './users.service';

@Component({
  selector: 'app-users',
  templateUrl: './users.html',
  styleUrl: './users.scss',
})
export class Users implements OnInit {
  private readonly usersService = inject(UsersService);

  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly roleLabels = ROLE_LABELS;

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);

    this.usersService.getAll().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }
}
