import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { parseApiError } from '../../core/api/api-error';
import { InventoryService } from '../../core/api/inventory.service';
import { AuthService } from '../../core/auth/auth.service';
import { InventoryCategory, InventoryItemDto } from '../../core/models/api.models';
import { DateField } from '../../shared/ui/date-field';
import { Segmented } from '../../shared/ui/segmented';

/**
 * Pharmacy & stores. The daily loop: reception dispenses/receives stock with
 * ± adjustments; low-stock and expiring items float to the top so ordering
 * happens before running out mid-treatment.
 */
@Component({
  selector: 'app-inventory',
  imports: [ReactiveFormsModule, FormsModule, RouterLink, DatePipe, DecimalPipe, DateField, Segmented],
  templateUrl: './inventory.html',
  styleUrl: './inventory.scss',
})
export class Inventory {
  private readonly api = inject(InventoryService);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);

  readonly categoryOptions = ['Medicine', 'Consumable', 'Equipment'];

  readonly loading = signal(true);
  readonly items = signal<InventoryItemDto[]>([]);
  readonly error = signal('');
  readonly notice = signal('');
  /** 402 from the API: inventory is a Clinic-plan feature — show the pitch. */
  readonly locked = signal(false);

  // ---- search ----
  private readonly search$ = new Subject<string>();
  searchTerm = '';

  // ---- add/edit drawer ----
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly editing = signal<InventoryItemDto | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    category: ['Medicine' as InventoryCategory, Validators.required],
    unit: ['strip', [Validators.required, Validators.maxLength(30)]],
    stockQuantity: [0, [Validators.required, Validators.min(0)]],
    reorderLevel: [5, [Validators.required, Validators.min(0)]],
    unitPriceRupees: [null as number | null, Validators.min(0)],
    expiryDate: [null as string | null],
  });

  // ---- per-row stock adjust popover ----
  readonly adjustFor = signal<string | null>(null);
  readonly adjusting = signal(false);
  adjustQty = 1;

  readonly canManage = this.auth.hasRole('Admin', 'Receptionist');

  constructor() {
    this.search$
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.load());
    this.load();
  }

  load(): void {
    this.error.set('');
    this.api.getAll(this.searchTerm.trim() || undefined).subscribe({
      next: (items) => {
        this.items.set(items);
        this.locked.set(false);
        this.loading.set(false);
      },
      error: (err) => {
        const parsed = parseApiError(err);
        if (parsed.status === 402) {
          // Not an error — a locked door with a nice handle
          this.locked.set(true);
        } else {
          this.error.set(parsed.message);
        }
        this.loading.set(false);
      },
    });
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.search$.next(term);
  }

  openAdd(): void {
    this.editing.set(null);
    this.formError.set('');
    this.form.reset({
      name: '', category: 'Medicine', unit: 'strip',
      stockQuantity: 0, reorderLevel: 5, unitPriceRupees: null, expiryDate: null,
    });
    this.drawerOpen.set(true);
  }

  openEdit(item: InventoryItemDto): void {
    this.editing.set(item);
    this.formError.set('');
    this.form.reset({
      name: item.name,
      category: item.category,
      unit: item.unit,
      stockQuantity: item.stockQuantity,   // display only when editing
      reorderLevel: item.reorderLevel,
      unitPriceRupees: item.unitPriceRupees,
      expiryDate: item.expiryDate,
    });
    this.drawerOpen.set(true);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.formError.set('');

    const value = this.form.getRawValue();
    const current = this.editing();
    const call = current
      ? this.api.update(current.id, value)
      : this.api.create(value);

    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.notice.set(current ? `${value.name} updated.` : `${value.name} added to inventory.`);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(parseApiError(err).message);
      },
    });
  }

  remove(item: InventoryItemDto): void {
    if (!confirm(`Remove "${item.name}" from inventory?`)) return;
    this.api.delete(item.id).subscribe({
      next: () => {
        // Close the drawer — the Remove button lives in it, and a stale
        // drawer would let "Save changes" hit a now-deleted record
        this.drawerOpen.set(false);
        this.editing.set(null);
        this.notice.set(`${item.name} removed.`);
        this.load();
      },
      error: (err) => this.error.set(parseApiError(err).message),
    });
  }

  toggleAdjust(item: InventoryItemDto): void {
    this.adjustQty = 1;
    this.adjustFor.update((id) => (id === item.id ? null : item.id));
  }

  applyAdjust(item: InventoryItemDto, direction: 1 | -1): void {
    const qty = Math.floor(Math.abs(this.adjustQty));
    if (!qty) return;
    this.adjusting.set(true);

    this.api.adjustStock(item.id, direction * qty).subscribe({
      next: (updated) => {
        this.items.update((list) => list.map((i) => (i.id === updated.id ? updated : i)));
        this.adjusting.set(false);
        this.adjustFor.set(null);
      },
      error: (err) => {
        this.adjusting.set(false);
        this.error.set(parseApiError(err).message);
        this.adjustFor.set(null);
      },
    });
  }

  errorFor(control: string): string {
    const c = this.form.get(control);
    if (!c || !c.touched || c.valid) return '';
    if (c.hasError('required')) return 'This field is required.';
    if (c.hasError('min')) return 'Cannot be negative.';
    return 'Invalid value.';
  }
}
