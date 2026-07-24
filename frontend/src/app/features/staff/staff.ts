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
  /** Page-level failures (load, resend) — shown red, never in the green banner. */
  readonly error = signal('');
  /** Member id whose invite is currently resending — blocks double-sends. */
  readonly resendingId = signal<string | null>(null);

  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});
  /** True when the API said 402: plan limit reached — show the upgrade path. */
  readonly upgradeNeeded = signal(false);
  /** Success message after adding — explains the visiting-doctor case. */
  readonly notice = signal('');

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: [''], // only required in temp-password mode (see setInviteMode)
  });

  /**
   * Two onboarding methods, because clinics differ:
   * - Invite by email (default): staff set their own password via the link
   * - Temporary password: for staff who don't use email — admin hands it over
   */
  readonly inviteMode = signal(true);

  setInviteMode(invite: boolean): void {
    this.inviteMode.set(invite);
    const password = this.form.controls.password;
    if (invite) {
      password.clearValidators();
      password.setValue('');
    } else {
      password.setValidators([Validators.required, Validators.minLength(8)]);
    }
    password.updateValueAndValidity();
  }

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
    this.error.set('');
    this.api.getAll().subscribe({
      next: (result) => {
        this.staff.set(result.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  openDrawer(): void {
    this.form.reset();
    this.selectedRoles.set(['Doctor']);
    this.setInviteMode(true);
    this.formError.set('');
    this.fieldErrors.set({});
    this.upgradeNeeded.set(false);
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

    const value = this.form.getRawValue();
    this.api.add({
      ...value,
      password: this.inviteMode() ? '' : value.password, // empty = invite-only
      roles: this.selectedRoles(),
    }).subscribe({
      next: (staff) => {
        this.saving.set(false);
        this.drawerOpen.set(false);
        this.notice.set(
          staff.existingAccount
            ? `${staff.fullName} already uses Klivia at another clinic — your clinic was added to their account. They sign in with their existing password and switch clinics from the sidebar.`
            : this.inviteMode()
              ? `Invitation sent to ${staff.email} — they'll set their own password from the email.`
              : `${staff.fullName} added. They can sign in with the temporary password (and also got an email to set their own).`,
        );
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

  resendInvite(member: StaffDto): void {
    if (this.resendingId()) return;   // one send at a time — each fires an email
    this.resendingId.set(member.id);
    this.error.set('');
    this.api.resendInvite(member.id).subscribe({
      next: () => {
        this.resendingId.set(null);
        this.notice.set(`A fresh invite (valid 7 days) was emailed to ${member.email}.`);
      },
      error: (err) => {
        this.resendingId.set(null);
        this.error.set(parseApiError(err).message);   // red banner, not the green one
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
