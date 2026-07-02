import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { StaffService } from '../../core/api/staff.service';
import { StaffDto } from '../../core/models/api.models';

@Component({
  selector: 'app-staff',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './staff.html',
})
export class Staff {
  private readonly api = inject(StaffService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly staff = signal<StaffDto[]>([]);

  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});
  /** True when the API said 402: plan limit reached — show the upgrade path. */
  readonly upgradeNeeded = signal(false);

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  // Roles combine: a partner who practices = Admin + Doctor
  readonly allRoles = ['Doctor', 'Receptionist', 'Admin'];
  readonly selectedRoles = signal<string[]>(['Doctor']);

  toggleRole(role: string): void {
    this.selectedRoles.update((current) =>
      current.includes(role) ? current.filter((r) => r !== role) : [...current, role],
    );
  }

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: (result) => {
        this.staff.set(result.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openDrawer(): void {
    this.form.reset();
    this.selectedRoles.set(['Doctor']);
    this.formError.set('');
    this.fieldErrors.set({});
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (this.selectedRoles().length === 0) {
      this.formError.set('Pick at least one role.');
      return;
    }

    this.saving.set(true);
    this.formError.set('');
    this.fieldErrors.set({});

    this.api.add({ ...this.form.getRawValue(), roles: this.selectedRoles() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.load();
      },
      error: (err) => {
        const parsed = parseApiError(err);
        this.formError.set(Object.keys(parsed.fieldErrors).length ? '' : parsed.message);
        this.fieldErrors.set(parsed.fieldErrors);
        this.upgradeNeeded.set(parsed.status === 402);
        this.saving.set(false);
      },
    });
  }

  errorFor(control: string): string {
    const c = this.form.get(control);
    if (c?.invalid && c.touched) {
      if (c.errors?.['required']) return 'This field is required.';
      if (c.errors?.['email']) return 'Enter a valid email address.';
      if (c.errors?.['minlength']) return 'At least 8 characters.';
    }
    return this.fieldErrors()[control] ?? '';
  }
}
