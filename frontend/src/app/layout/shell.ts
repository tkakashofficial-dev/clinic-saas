import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { parseApiError } from '../core/api/api-error';
import { BillingService } from '../core/api/billing.service';
import { NotificationsService } from '../core/api/notifications.service';
import { AuthService } from '../core/auth/auth.service';
import { PwaInstallService } from '../core/pwa-install.service';
import { PwaUpdateService } from '../core/pwa-update.service';
import { NotificationDto, Role } from '../core/models/api.models';
import { NEW_CLINIC_HINT, OnboardingTour } from '../shared/ui/onboarding-tour';
import { ProvisioningOverlay } from '../shared/ui/provisioning-overlay';

const NEW_CLINIC_HINT_KEY = 'klivia.hint.newclinic';

interface NavItem {
  label: string;
  path: string;
  icon: 'dashboard' | 'patients' | 'appointments' | 'invoices' | 'inventory' | 'forms' | 'staff' | 'reports' | 'billing' | 'settings' | 'platform';
  roles: Role[];
  /** Only the SaaS owner sees this, regardless of clinic roles. */
  platformOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: 'dashboard', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Patients', path: '/patients', icon: 'patients', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Appointments', path: '/appointments', icon: 'appointments', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Invoices', path: '/invoices', icon: 'invoices', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Inventory', path: '/inventory', icon: 'inventory', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Forms', path: '/forms', icon: 'forms', roles: ['Admin', 'Doctor', 'Receptionist'] },
  { label: 'Reports', path: '/reports', icon: 'reports', roles: ['Admin'] },
  { label: 'Staff', path: '/staff', icon: 'staff', roles: ['Admin'] },
  { label: 'Billing', path: '/billing', icon: 'billing', roles: ['Admin'] },
  { label: 'Settings', path: '/settings', icon: 'settings', roles: ['Admin'] },
  { label: 'Platform', path: '/platform', icon: 'platform', roles: [], platformOnly: true },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, DatePipe, ProvisioningOverlay, OnboardingTour],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationsService);
  readonly billing = inject(BillingService);
  readonly pwa = inject(PwaInstallService);
  readonly pwaUpdate = inject(PwaUpdateService);
  private readonly router = inject(Router);

  readonly navItems = computed(() => {
    const roles = this.auth.roles();
    return NAV_ITEMS.filter((item) =>
      item.platformOnly
        ? this.auth.isPlatformAdmin()
        : item.roles.some((role) => roles.includes(role)),
    );
  });

  // Phones get a thumb-friendly bottom tab bar: first four destinations
  // as tabs, the rest behind "More" (the iOS/Android convention)
  readonly mobileNavItems = computed(() => this.navItems().slice(0, 4));
  readonly moreNavItems = computed(() => this.navItems().slice(4));
  readonly moreOpen = signal(false);

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
  readonly clinicInitials = computed(() => this.clinicInitialsOf(this.clinicDisplayName()));

  clinicInitialsOf(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  // ---- clinic switcher (opening NEW clinics is an owner move — Admin only) ----
  readonly switcherOpen = signal(false);
  readonly newClinicOpen = signal(false);
  readonly creating = signal(false);
  readonly newClinicError = signal('');
  /** 402 from the API: multi-clinic needs Growth — show the upgrade path. */
  readonly newClinicUpgradeNeeded = signal(false);
  /** Non-null while a clinic is being provisioned — drives the overlay. */
  readonly provisioningClinic = signal<string | null>(null);
  newClinicName = '';
  newClinicIsDoctor = true;

  readonly canCreateClinic = computed(() => this.auth.hasRole('Admin'));
  readonly canOpenSwitcher = computed(
    () => this.auth.memberships().length > 1 || this.canCreateClinic(),
  );
  /** Multi-clinic is Growth-only. Already-multi owners obviously qualify. */
  readonly canMultiClinic = computed(
    () => this.billing.summary()?.plan === 'Growth' || this.auth.memberships().length > 1,
  );

  // ---- topbar: notifications + profile ----
  readonly notifOpen = signal(false);
  readonly notifLoading = signal(false);
  readonly notifItems = signal<NotificationDto[]>([]);
  readonly profileOpen = signal(false);

  // Trial banner for owners — reads the SHARED billing signal, so a plan
  // change on the billing page updates this chip immediately
  readonly trialDaysLeft = computed(() => {
    const summary = this.billing.summary();
    if (!summary?.isInTrial || !summary.trialEndsAt) return null;
    const ms = new Date(summary.trialEndsAt).getTime() - Date.now();
    return Math.max(0, Math.ceil(ms / 86_400_000));
  });

  /** One-time coachmark on the switcher right after opening a new clinic. */
  readonly newClinicHint = NEW_CLINIC_HINT;
  readonly showNewClinicHint = signal(false);

  constructor() {
    this.notifications.startPolling();
    if (this.auth.hasRole('Admin')) {
      this.billing.getSummary().subscribe({ error: () => {} });
    }

    if (localStorage.getItem(NEW_CLINIC_HINT_KEY)) {
      localStorage.removeItem(NEW_CLINIC_HINT_KEY);
      // Let the shell paint first so the spotlight can find the switcher
      setTimeout(() => this.showNewClinicHint.set(true), 600);
    }
  }

  switchTo(tenantId: string): void {
    this.switcherOpen.set(false);
    if (tenantId !== this.auth.currentTenantId()) {
      this.auth.switchClinic(tenantId);
    }
  }

  openNewClinic(): void {
    this.switcherOpen.set(false);
    this.moreOpen.set(false);

    // Locked on Solo/Clinic plans: the tap goes straight to the pitch
    if (!this.canMultiClinic()) {
      void this.router.navigate(['/billing']);
      return;
    }

    this.newClinicName = '';
    this.newClinicIsDoctor = true;
    this.newClinicError.set('');
    this.newClinicUpgradeNeeded.set(false);
    this.newClinicOpen.set(true);
  }

  createClinic(): void {
    const name = this.newClinicName.trim();
    if (!name) return;

    this.creating.set(true);
    this.newClinicError.set('');
    this.newClinicOpen.set(false);
    this.provisioningClinic.set(name);   // full-screen "building your clinic" experience

    this.auth.createClinic(name, this.newClinicIsDoctor).subscribe({
      next: (response) => {
        // The new clinic shows the "you are here" switcher hint, not the
        // full 8-step tour (they've seen the product already)
        localStorage.setItem(`klivia.tour.${response.tenantId}`, 'done');
        localStorage.setItem(NEW_CLINIC_HINT_KEY, '1');
        location.assign('/');            // overlay stays up until the reload lands
      },
      error: (err) => {
        // NEVER fail silently: hide the overlay, reopen the drawer WITH the reason
        const parsed = parseApiError(err);
        this.provisioningClinic.set(null);
        this.creating.set(false);
        this.newClinicError.set(parsed.message);
        this.newClinicUpgradeNeeded.set(parsed.status === 402);
        this.newClinicOpen.set(true);
      },
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

  // ---- PWA install ----
  /** Manual instructions for browsers without a prompt API (iOS Safari…). */
  readonly installHelpOpen = signal(false);

  installApp(): void {
    this.profileOpen.set(false);
    this.moreOpen.set(false);

    // Chrome/Edge handed us a real prompt — show it right now.
    // Otherwise (iOS, Firefox, or Chrome not ready yet) show the steps.
    if (this.pwa.canInstall()) {
      void this.pwa.promptInstall();
    } else {
      this.installHelpOpen.set(true);
    }
  }

  replayTour(): void {
    localStorage.removeItem(`klivia.tour.${this.auth.currentTenantId()}`);
    location.assign('/dashboard'); // fresh load → dashboard shows the tour again
  }

  logout(): void {
    this.notifications.stopPolling();
    this.auth.logout();
  }
}
