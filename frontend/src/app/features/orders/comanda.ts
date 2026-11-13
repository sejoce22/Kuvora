import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Order, OrderItem } from './orders.models';
import { OrdersService } from './orders.service';

// Comanda imprimible de una ronda: papel de 80 mm que sirve en una impresora térmica instalada como normal.
@Component({
  selector: 'app-comanda',
  imports: [DatePipe, RouterLink],
  templateUrl: './comanda.html',
  styleUrl: './comanda.scss',
})
export class Comanda implements OnInit {
  private readonly orders = inject(OrdersService);
  private readonly params = inject(ActivatedRoute).snapshot.paramMap;

  protected readonly orderId = this.params.get('id') ?? '';
  protected readonly roundNumber = Number(this.params.get('round'));

  protected readonly order = signal<Order | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  // Ítems de esta ronda. Los cancelados después se imprimen marcados: cocina debe saber que ya no van.
  protected readonly items = computed<OrderItem[]>(
    () =>
      this.order()?.items.filter(
        (item) => item.round === this.roundNumber && (item.status === 'Sent' || item.status === 'Cancelled'),
      ) ?? [],
  );

  protected readonly sentAtUtc = computed(() => this.items().find((item) => item.sentAtUtc)?.sentAtUtc ?? null);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.orders.get(this.orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        if (this.items().length === 0) this.error.set('Esta ronda no existe en el pedido.');
      },
      error: (error: { status?: number }) => {
        this.loading.set(false);
        this.error.set(error.status === 404 ? 'No encontramos este pedido.' : 'No pudimos cargar la comanda.');
      },
    });
  }

  protected print(): void {
    window.print();
  }
}
