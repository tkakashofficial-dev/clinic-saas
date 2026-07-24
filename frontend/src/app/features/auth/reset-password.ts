import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { parseApiError } from '../../core/api/api-error';
import { PasswordInput } from '../../shared/ui/password-input';

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, PasswordInput],
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
  /** True once the API rejects the token (expired/already used) — offer a new link. */
  readonly tokenRejected = signal(false);
  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8),
      Validators.pattern(/^(?=.*[A-Za-z])(?=.*[0-9]).*$/)]],
    confirm: ['', Validators.required],
  });

  /** Show mismatch only after the user has typed a confirm value. */
  mismatch(): boolean {
    const { newPassword, confirm } = this.form.getRawValue();
    return !!confirm && this.form.controls.confirm.touched && newPassword !== confirm;
  }

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
          const parsed = parseApiError(err);
          // 400/401 = token expired or already used — a dead form, so pivot
          // the whole screen to a "request a fresh link" call to action
          if (parsed.status === 400 || parsed.status === 401) this.tokenRejected.set(true);
          this.error.set(parsed.fieldErrors['newPassword'] ?? parsed.message);
          this.loading.set(false);
        },
      });
  }
}
