import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { parseApiError } from '../../core/api/api-error';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './auth-layout.scss',
})
export class ForgotPassword {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly sent = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.http
      .post(`${environment.apiUrl}/auth/forgot-password`, this.form.getRawValue())
      .subscribe({
        // Success is deliberately vague — the API never reveals if the email
        // exists (no account enumeration)
        next: () => {
          this.loading.set(false);
          this.sent.set(true);
        },
        error: (err) => {
          // But a network/server failure must NOT masquerade as "check your
          // inbox" — the mail was never sent
          this.loading.set(false);
          const status = parseApiError(err).status;
          if (status === 0 || status >= 500) {
            this.error.set("Couldn't reach the server — check your connection and try again.");
          } else {
            this.sent.set(true);   // 4xx (incl. rate-limit) stays vague
          }
        },
      });
  }
}
