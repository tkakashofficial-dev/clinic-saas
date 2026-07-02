import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { parseApiError } from '../../core/api/api-error';
import { PatientsService } from '../../core/api/patients.service';
import { AuthService } from '../../core/auth/auth.service';
import { PagedResult, PatientDto } from '../../core/models/api.models';

@Component({
  selector: 'app-patients',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './patients.html',
})
export class Patients {
  readonly auth = inject(AuthService);
  private readonly api = inject(PatientsService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly result = signal<PagedResult<PatientDto> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);

  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});

  private readonly searchInput$ = new Subject<string>();

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phone: ['', Validators.required],
    email: [''],
    address: [''],
    gender: ['Male', Validators.required],
    dateOfBirth: [''],
  });

  constructor() {
    // Debounce keystrokes — search fires 300ms after typing stops,
    // not on every letter
    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => {
        this.search.set(term);
        this.page.set(1);
        this.load();
      });

    this.load();
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  load(): void {
    this.loading.set(true);
    this.api.getAll(this.search(), this.page()).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  openDrawer(): void {
    this.form.reset({ gender: 'Male' });
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

    this.saving.set(true);
    this.formError.set('');
    this.fieldErrors.set({});

    const value = this.form.getRawValue();
    this.api
      .register({
        firstName: value.firstName,
        lastName: value.lastName,
        phone: value.phone,
        email: value.email || null,
        address: value.address || null,
        gender: value.gender,
        dateOfBirth: value.dateOfBirth || null,
        medicalConditionCodes: [],
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.drawerOpen.set(false);
          this.load();
        },
        error: (err) => {
          const parsed = parseApiError(err);
          this.formError.set(Object.keys(parsed.fieldErrors).length ? '' : parsed.message);
          this.fieldErrors.set(parsed.fieldErrors);
          this.saving.set(false);
        },
      });
  }

  errorFor(control: string): string {
    const c = this.form.get(control);
    if (c?.invalid && c.touched && c.errors?.['required']) return 'This field is required.';
    return this.fieldErrors()[control] ?? '';
  }
}
