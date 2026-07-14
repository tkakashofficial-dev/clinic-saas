import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { parseApiError } from '../../core/api/api-error';
import { PlatformService } from '../../core/api/platform.service';
import { PlatformTenant } from '../../core/models/api.models';

/**
 * The SaaS owner's back office: every clinic on the platform, its plan, trial
 * state and usage. Payments are collected manually (UPI / bank transfer) for
 * now — once money lands, the owner applies the plan here. Suspend cuts off
 * sign-in for a clinic that stopped paying; nothing is deleted.
 */
@Component({
  selector: 'app-platform',
  imports: [DatePipe],
  templateUrl: './platform.html',
  styleUrl: './platform.scss',
})
export class Platform {
  private readonly api = inject(PlatformService);

  readonly plans = ['Solo', 'Clinic', 'Growth'] as const;

  readonly loading = signal(true);
  readonly tenants = signal<PlatformTenant[]>([]);
  readonly error = signal('');
  readonly notice = signal('');
  /** tenantId currently saving — disables that row's controls. */
  readonly busy = signal<string | null>(null);
  /** tenantId with the plan menu open. */
  readonly planMenuFor = signal<string | null>(null);

  readonly totalClinics = computed(() => this.tenants().length);
  readonly activeClinics = computed(() => this.tenants().filter((t) => t.isActive).length);
  readonly trialClinics = computed(() => this.tenants().filter((t) => t.isInTrial).length);
  readonly paidClinics = computed(
    () => this.tenants().filter((t) => !t.isInTrial && t.plan !== 'Solo' && t.isActive).length,
  );

  constructor() {
    this.load();
  }

  load(): void {
    this.api.getTenants().subscribe({
      next: (tenants) => {
        this.tenants.set(tenants);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  togglePlanMenu(tenant: PlatformTenant): void {
    this.planMenuFor.update((id) => (id === tenant.tenantId ? null : tenant.tenantId));
  }

  changePlan(tenant: PlatformTenant, plan: string): void {
    this.planMenuFor.set(null);
    if (plan === tenant.plan) return;
    this.mutate(tenant.tenantId, this.api.changePlan(tenant.tenantId, plan),
      `${tenant.name} moved to the ${plan} plan.`);
  }

  toggleActive(tenant: PlatformTenant): void {
    const target = !tenant.isActive;
    this.mutate(tenant.tenantId, this.api.setActive(tenant.tenantId, target),
      target
        ? `${tenant.name} re-activated — staff can sign in again.`
        : `${tenant.name} suspended — sign-in is blocked until re-activated.`);
  }

  private mutate(
    tenantId: string,
    call: ReturnType<PlatformService['changePlan']>,
    successMessage: string,
  ): void {
    this.error.set('');
    this.notice.set('');
    this.busy.set(tenantId);

    call.subscribe({
      next: (updated) => {
        this.tenants.update((list) =>
          list.map((t) => (t.tenantId === updated.tenantId ? updated : t)));
        this.busy.set(null);
        this.notice.set(successMessage);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.busy.set(null);
      },
    });
  }
}
