import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NotificationsService } from '../core/api/notifications.service';
import { AuthService } from '../core/auth/auth.service';
import { NotificationDto, Role } from '../core/models/api.models';

interface NavItem {
  label: string;
  path: string;
  icon: 'dashboard' | 'patients' | 'appointments' | 'staff' | 'reports';
  roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Patients', path: '/patients', icon: 'patients', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Appointments', path: '/appointments', icon: 'appointments', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Reports', path: '/reports', icon: 'reports', roles: ['Admin'] },
  { label: 'Staff', path: '/staff', icon: 'staff', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, DatePipe],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationsService);

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

  // The CLINIC is the hero of the sidebar — the product is just "powered by".
  // Falls back to the product name for sessions from before clinic branding.
  readonly clinicDisplayName = computed(() => this.auth.clinicName() || 'My Clinic');
  readonly clinicInitials = computed(() =>
    this.clinicDisplayName()
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
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

  readonly canSwitch = computed(() => this.auth.memberships().length > 0);

  // ---- notifications ----
  readonly notifOpen = signal(false);
  readonly notifLoading = signal(false);
  readonly notifItems = signal<NotificationDto[]>([]);

  constructor() {
    this.notifications.startPolling();
  }

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
      next: () => location.assign('/'),
      error: () => this.creating.set(false),
    });
  }

  openNotifications(): void {
    this.notifOpen.set(true);
    this.notifLoading.set(true);
    this.notifications.getMine(1, 30).subscribe({
      next: (result) => {
        this.notifItems.set(result.items);
        this.notifLoading.set(false);
      },
      error: () => this.notifLoading.set(false),
    });
  }

  markAllRead(): void {
    this.notifications.markAllRead().subscribe({
      next: () => {
        this.notifItems.update((items) => items.map((n) => ({ ...n, isRead: true })));
        this.notifications.unreadCount.set(0);
      },
    });
  }

  logout(): void {
    this.notifications.stopPolling();
    this.auth.logout();
  }
}
