import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { parseApiError } from '../../core/api/api-error';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './auth-layout.scss',
})
export class ResetPassword {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly done = signal(false);
  readonly error = signal('');
  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirm: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { newPassword, confirm } = this.form.getRawValue();
    if (newPassword !== confirm) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.http
      .post(`${environment.apiUrl}/auth/reset-password`, { token: this.token, newPassword })
      .subscribe({
        next: () => this.done.set(true),
        error: (err) => {
          this.error.set(parseApiError(err).message);
          this.loading.set(false);
        },
      });
  }
}
