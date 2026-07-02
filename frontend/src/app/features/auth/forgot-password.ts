import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';

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

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.http
      .post(`${environment.apiUrl}/auth/forgot-password`, this.form.getRawValue())
      .subscribe({
        // Same outcome either way — the API never reveals if the email exists
        next: () => this.sent.set(true),
        error: () => this.sent.set(true),
      });
  }
}
