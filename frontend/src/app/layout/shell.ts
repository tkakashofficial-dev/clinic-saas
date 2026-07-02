import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { Role } from '../core/models/api.models';

interface NavItem {
  label: string;
  path: string;
  icon: 'dashboard' | 'patients' | 'appointments' | 'staff';
  roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Patients', path: '/patients', icon: 'patients', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Appointments', path: '/appointments', icon: 'appointments', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Staff', path: '/staff', icon: 'staff', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);

  readonly navItems = computed(() => {
    const roles = this.auth.roles();
    return NAV_ITEMS.filter((item) => item.roles.some((role) => roles.includes(role)));
  });

  readonly initials = computed(() =>
    this.auth
      .fullName()
      .split(' ')
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  // ---- clinic switcher ----
  readonly switcherOpen = signal(false);
  readonly newClinicOpen = signal(false);
  readonly creating = signal(false);
  newClinicName = '';
  newClinicIsDoctor = true;

  readonly showSwitcher = computed(
    () => this.auth.memberships().length > 0 && this.auth.clinicName() !== '',
  );

  switchTo(tenantId: string): void {
    this.switcherOpen.set(false);
    if (tenantId !== this.auth.currentTenantId()) {
      this.auth.switchClinic(tenantId);
    }
  }

  openNewClinic(): void {
    this.newClinicName = '';
    this.newClinicIsDoctor = true;
    this.switcherOpen.set(false);
    this.newClinicOpen.set(true);
  }

  createClinic(): void {
    if (!this.newClinicName.trim()) return;
    this.creating.set(true);
    this.auth.createClinic(this.newClinicName.trim(), this.newClinicIsDoctor).subscribe({
      next: () => location.assign('/'), // land in the new clinic, fresh state
      error: () => this.creating.set(false),
    });
  }

  logout(): void {
    this.auth.logout();
  }
}
