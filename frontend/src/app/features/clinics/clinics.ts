import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { ProvisioningOverlay } from '../../shared/ui/provisioning-overlay';

/**
 * "My clinics" — the structure view: every clinic under this login as a
 * card, plus the place to open a new one. Multi-clinic is Growth-only,
 * enforced by the API; the 402 here becomes an upgrade prompt.
 */
@Component({
  selector: 'app-clinics',
  imports: [FormsModule, RouterLink, ProvisioningOverlay],
  templateUrl: './clinics.html',
  styleUrl: './clinics.scss',
})
export class Clinics {
  readonly auth = inject(AuthService);

  readonly canCreate = computed(() => this.auth.hasRole('Admin'));

  readonly createOpen = signal(false);
  readonly creating = signal(false);
  readonly error = signal('');
  readonly upgradeNeeded = signal(false);
  readonly provisioningClinic = signal<string | null>(null);
  readonly switchingTo = signal<string | null>(null);

  newName = '';
  newIsDoctor = true;

  initialsOf(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  switchTo(tenantId: string): void {
    if (tenantId === this.auth.currentTenantId()) return;
    this.switchingTo.set(tenantId);
    this.auth.switchClinic(tenantId); // full reload on success
  }

  openCreate(): void {
    this.newName = '';
    this.newIsDoctor = true;
    this.error.set('');
    this.upgradeNeeded.set(false);
    this.createOpen.set(true);
  }

  create(): void {
    const name = this.newName.trim();
    if (!name) return;

    this.creating.set(true);
    this.error.set('');
    this.createOpen.set(false);
    this.provisioningClinic.set(name);

    this.auth.createClinic(name, this.newIsDoctor).subscribe({
      next: (response) => {
        localStorage.setItem(`klivia.tour.${response.tenantId}`, 'done');
        localStorage.setItem('klivia.hint.newclinic', '1');
        location.assign('/');
      },
      error: (err) => {
        const parsed = parseApiError(err);
        this.provisioningClinic.set(null);
        this.creating.set(false);
        this.error.set(parsed.message);
        this.upgradeNeeded.set(parsed.status === 402);
        this.createOpen.set(true);
      },
    });
  }
}
