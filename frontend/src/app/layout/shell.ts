import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BillingService } from '../core/api/billing.service';
import { NotificationsService } from '../core/api/notifications.service';
import { AuthService } from '../core/auth/auth.service';
import { BillingSummary, NotificationDto, Role } from '../core/models/api.models';

interface NavItem {
  label: string;
  path: string;
  icon: 'dashboard' | 'patients' | 'appointments' | 'staff' | 'reports' | 'billing';
  roles: Role[];
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Patients', path: '/patients', icon: 'patients', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Appointments', path: '/appointments', icon: 'appointments', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Reports', path: '/reports', icon: 'reports', roles: ['Admin'] },
  { label: 'Staff', path: '/staff', icon: 'staff', roles: ['Admin'] },
  { label: 'Billing', path: '/billing', icon: 'billing', roles: ['Admin'] },
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
  private readonly billing = inject(BillingService);

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

  // ---- clinic switcher (opening NEW clinics is an owner move — Admin only) ----
  readonly switcherOpen = signal(false);
  readonly newClinicOpen = signal(false);
  readonly creating = signal(false);
  newClinicName = '';
  newClinicIsDoctor = true;

  readonly canCreateClinic = computed(() => this.auth.hasRole('Admin'));
  readonly canOpenSwitcher = computed(
    () => this.auth.memberships().length > 1 || this.canCreateClinic(),
  );

  // ---- topbar: notifications + profile ----
  readonly notifOpen = signal(false);
  readonly notifLoading = signal(false);
  readonly notifItems = signal<NotificationDto[]>([]);
  readonly profileOpen = signal(false);

  // Trial banner for owners
  readonly billingSummary = signal<BillingSummary | null>(null);
  readonly trialDaysLeft = computed(() => {
    const summary = this.billingSummary();
    if (!summary?.isInTrial || !summary.trialEndsAt) return null;
    const ms = new Date(summary.trialEndsAt).getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / 86_400_000));
  });

  constructor() {
    this.notifications.startPolling();
    if (this.auth.hasRole('Admin')) {
      this.billing.getSummary().subscribe({
        next: (summary) => this.billingSummary.set(summary),
        error: () => {},
      });
    }
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
    this.profileOpen.set(false);
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

  toggleProfile(): void {
    this.profileOpen.update((open) => !open);
  }

  logout(): void {
    this.notifications.stopPolling();
    this.auth.logout();
  }
}
