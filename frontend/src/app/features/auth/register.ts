import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './auth-layout.scss',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly form = this.fb.nonNullable.group({
    clinicName: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    ownerIsDoctor: [true], // most small-clinic owners are the practicing doctor
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.fieldErrors.set({});

    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigate(['/']),
      error: (err) => {
        const parsed = parseApiError(err);
        this.error.set(Object.keys(parsed.fieldErrors).length ? '' : parsed.message);
        this.fieldErrors.set(parsed.fieldErrors);
        this.loading.set(false);
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
