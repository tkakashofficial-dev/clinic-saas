import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { PasswordInput } from '../../shared/ui/password-input';
import { ProvisioningOverlay } from '../../shared/ui/provisioning-overlay';

/** Group validator: confirm must equal password. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, ProvisioningOverlay, PasswordInput],
  templateUrl: './register.html',
  styleUrl: './auth-layout.scss',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});
  /** Non-empty while the clinic is being provisioned — drives the overlay. */
  readonly provisioningName = signal('');

  /**
   * Two calm steps instead of one wall of fields:
   * 1. YOU (account) → 2. YOUR CLINIC. One API call at the end.
   */
  readonly step = signal<1 | 2>(1);

  /** Enter key / form submit: advance on step 1, register on step 2. */
  onSubmit(): void {
    if (this.step() === 1) this.continueToClinic();
    else this.submit();
  }

  continueToClinic(): void {
    const account = ['firstName', 'lastName', 'email', 'password', 'confirmPassword'] as const;
    account.forEach((name) => this.form.controls[name].markAsTouched());
    const fieldsValid = account.every((name) => this.form.controls[name].valid);
    // The match check lives on the group, not a single control
    if (fieldsValid && !this.form.errors?.['passwordMismatch']) this.step.set(2);
  }

  back(): void {
    this.step.set(1);
  }

  /** Show the mismatch error only once confirm has been touched. */
  showMismatch(): boolean {
    const confirm = this.form.controls.confirmPassword;
    return !!this.form.errors?.['passwordMismatch'] && confirm.touched;
  }

  readonly form = this.fb.nonNullable.group({
    clinicName: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    // Mirror the backend rule (8+ chars incl. a letter AND a digit) so typos
    // are caught before submit, not bounced by the API
    password: ['', [Validators.required, Validators.minLength(8),
      Validators.pattern(/^(?=.*[A-Za-z])(?=.*[0-9]).*$/)]],
    confirmPassword: ['', Validators.required],
    ownerIsDoctor: [true], // most small-clinic owners are the practicing doctor
  }, { validators: passwordsMatch });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.fieldErrors.set({});

    // The wait can be long (cold server) — show the "building your clinic"
    // experience instead of a frozen button
    const { confirmPassword, ...value } = this.form.getRawValue();
    this.provisioningName.set(value.clinicName.trim() || 'your clinic');

    this.auth.register(value).subscribe({
      next: () => {
        window.location.assign('/dashboard'); // overlay stays up until the app loads
      },
      error: (err) => {
        this.provisioningName.set('');
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
      if (c.errors?.['pattern']) return 'At least 8 characters, with a letter and a digit.';
    }
    return this.fieldErrors()[control] ?? '';
  }
}
