import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { PasswordInput } from '../../shared/ui/password-input';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, PasswordInput],
  templateUrl: './login.html',
  styleUrl: './auth-layout.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigate(['/']),
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  invalid(control: 'email' | 'password'): boolean {
    const c = this.form.controls[control];
    return c.invalid && c.touched;
  }
}
