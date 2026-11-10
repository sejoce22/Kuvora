import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/http/problem-details';
import {
  DiningTable,
  TABLE_MAX_CAPACITY,
  TABLE_MIN_CAPACITY,
  TABLE_NAME_MAX,
  TABLE_ZONE_MAX,
  ZoneGroup,
} from './tables.models';
import { TablesService } from './tables.service';

// Orden "natural": Mesa 2 antes que Mesa 10 (el backend ordena como texto y deja Mesa 10 antes).
const collator = new Intl.Collator('es', { numeric: true, sensitivity: 'base' });

@Component({
  selector: 'app-tables',
  imports: [ReactiveFormsModule],
  templateUrl: './tables.html',
  styleUrl: './tables.scss',
})
export class Tables implements OnInit {
  private readonly tablesService = inject(TablesService);
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly tables = signal<DiningTable[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  // undefined = formulario cerrado; null = mesa nueva; string = id de la mesa que se edita
  protected readonly editingId = signal<string | null | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  // La interfaz solo muestra los botones de edición al Admin; la seguridad real está en el backend (403).
  protected readonly canEdit = computed(() => this.auth.user()?.role === 'Admin');

  // Mesas agrupadas por zona: zonas en orden alfabético ("Sin zona" al final) y mesas en orden natural.
  protected readonly zones = computed<ZoneGroup[]>(() => {
    const groups = new Map<string | null, DiningTable[]>();

    for (const table of this.tables()) {
      const key = table.zone?.trim() || null;
      const list = groups.get(key);
      if (list) {
        list.push(table);
      } else {
        groups.set(key, [table]);
      }
    }

    return [...groups.entries()]
      .map(([zone, tables]) => ({
        zone,
        tables: [...tables].sort((a, b) => collator.compare(a.name, b.name)),
      }))
      .sort((a, b) => {
        if (a.zone === null) return 1;
        if (b.zone === null) return -1;
        return collator.compare(a.zone, b.zone);
      });
  });

  // Zonas que ya existen: se ofrecen como sugerencia al escribir una zona (evita "Terraza" y "terraza ")
  protected readonly zoneNames = computed(() =>
    this.zones()
      .map((group) => group.zone)
      .filter((zone): zone is string => zone !== null),
  );

  protected readonly tableNameMax = TABLE_NAME_MAX;
  protected readonly tableZoneMax = TABLE_ZONE_MAX;
  protected readonly minCapacity = TABLE_MIN_CAPACITY;
  protected readonly maxCapacity = TABLE_MAX_CAPACITY;

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(TABLE_NAME_MAX)]],
    capacity: [
      4,
      [
        Validators.required,
        Validators.min(TABLE_MIN_CAPACITY),
        Validators.max(TABLE_MAX_CAPACITY),
        Validators.pattern(/^\d+$/),
      ],
    ],
    zone: ['', [Validators.maxLength(TABLE_ZONE_MAX)]],
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.fetch(() => this.loading.set(false), () => this.failed.set(true));
  }

  // ---- Formulario ----

  protected newTable(): void {
    this.form.reset({ name: '', capacity: 4, zone: '' });
    this.openEditor(null);
  }

  protected editTable(table: DiningTable): void {
    this.form.reset({ name: table.name, capacity: table.capacity, zone: table.zone ?? '' });
    this.openEditor(table.id);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
    this.formError.set(null);
  }

  protected save(): void {
    const id = this.editingId();
    if (this.form.invalid || id === undefined) {
      this.form.markAllAsTouched();
      return;
    }

    const { zone, ...rest } = this.form.getRawValue();
    // Zona vacía = sin zona (null), como espera la API
    const request = { ...rest, zone: zone.trim() === '' ? null : zone.trim() };
    const call = id === null ? this.tablesService.create(request) : this.tablesService.update(id, request);

    this.saving.set(true);
    this.formError.set(null);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.cancel();
        this.refresh();
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.formError.set(problemMessage(error, 'No pudimos guardar la mesa.'));
      },
    });
  }

  protected toggle(table: DiningTable): void {
    this.actionError.set(null);
    this.tablesService.setActive(table.id, !table.isActive).subscribe({
      next: () => this.refresh(),
      error: (error: HttpErrorResponse) =>
        this.actionError.set(problemMessage(error, 'No pudimos cambiar el estado de la mesa.')),
    });
  }

  // ---- Internos ----

  private openEditor(id: string | null): void {
    this.formError.set(null);
    this.actionError.set(null);
    this.editingId.set(id);
  }

  // Recarga en silencio (sin el aviso "Cargando…") después de guardar o cambiar un estado
  private refresh(): void {
    this.fetch(
      () => undefined,
      () => this.actionError.set('No pudimos actualizar la lista. Recarga la página.'),
    );
  }

  // El Admin también pide las mesas inactivas.
  private fetch(onDone: () => void, onError: () => void): void {
    this.tablesService.getAll(this.canEdit()).subscribe({
      next: (tables) => {
        this.tables.set(tables);
        onDone();
      },
      error: () => {
        onError();
        onDone();
      },
    });
  }
}
