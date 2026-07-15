import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { parseApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { PasswordInput } from '../../shared/ui/password-input';

interface InviteInfo {
  firstName: string;
  email: string;
  clinicNames: string[];
}

/**
 * The invited-staff experience: "Join {clinic}" — email shown read-only
 * (it's how the invite found them), they only choose a password, and on
 * success we sign them in automatically. No login screen after joining.
 */
@Component({
  selector: 'app-accept-invite',
  imports: [ReactiveFormsModule, RouterLink, PasswordInput],
  templateUrl: './accept-invite.html',
  styleUrl: './auth-layout.scss',
})
export class AcceptInvite {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  readonly loadingInfo = signal(true);
  readonly invite = signal<InviteInfo | null>(null);
  readonly invalidInvite = signal('');
  readonly saving = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirm: ['', Validators.required],
  });

  constructor() {
    if (!this.token) {
      this.invalidInvite.set('This invite link is incomplete.');
      this.loadingInfo.set(false);
      return;
    }

    this.http
      .get<InviteInfo>(`${environment.apiUrl}/auth/invite-info`, {
        params: { token: this.token },
      })
      .subscribe({
        next: (info) => {
          this.invite.set(info);
          this.loadingInfo.set(false);
        },
        error: (err) => {
          this.invalidInvite.set(parseApiError(err).message);
          this.loadingInfo.set(false);
        },
      });
  }

  clinicLabel(): string {
    const names = this.invite()?.clinicNames ?? [];
    return names.length > 0 ? names[names.length - 1] : 'your clinic';
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { password, confirm } = this.form.getRawValue();
    if (password !== confirm) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.saving.set(true);
    this.error.set('');

    this.http
      .post(`${environment.apiUrl}/auth/reset-password`, {
        token: this.token,
        newPassword: password,
      })
      .subscribe({
        next: () => {
          // Auto sign-in: joining should end INSIDE the clinic, not at a login form
          this.auth
            .login({ email: this.invite()!.email, password })
            .subscribe({
              next: () => location.assign('/dashboard'),
              error: () => location.assign('/login'), // fallback: account works, just sign in
            });
        },
        error: (err) => {
          this.error.set(parseApiError(err).message);
          this.saving.set(false);
        },
      });
  }
}
