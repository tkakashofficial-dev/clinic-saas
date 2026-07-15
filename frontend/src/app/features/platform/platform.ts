import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { parseApiError } from '../../core/api/api-error';
import { PlatformService } from '../../core/api/platform.service';
import { PaymentMethod, PlatformPayment, PlatformTenant } from '../../core/models/api.models';
import { DateField } from '../../shared/ui/date-field';

/**
 * The SaaS owner's back office: every clinic on the platform, its plan, trial
 * state and usage. Payments are collected manually (UPI / bank transfer) for
 * now — once money lands, the owner applies the plan here. Suspend cuts off
 * sign-in for a clinic that stopped paying; nothing is deleted.
 */
@Component({
  selector: 'app-platform',
  imports: [DatePipe, DecimalPipe, FormsModule, DateField],
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
  readonly overdueClinics = computed(
    () => this.tenants().filter((t) => t.paymentOverdue).length,
  );

  /** wa.me link from the clinic phone (Indian default country code). */
  waLink(phone: string): string {
    let digits = phone.replace(/\D/g, '');
    if (digits.length === 10) digits = '91' + digits;
    return `https://wa.me/${digits}`;
  }

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

  // ---- tenant detail drawer: contact + payment history ----
  readonly detailFor = signal<PlatformTenant | null>(null);
  readonly payments = signal<PlatformPayment[]>([]);
  readonly paymentsLoading = signal(false);

  openDetail(tenant: PlatformTenant): void {
    this.detailFor.set(tenant);
    this.payments.set([]);
    this.paymentsLoading.set(true);
    this.api.getPayments(tenant.tenantId).subscribe({
      next: (payments) => {
        this.payments.set(payments);
        this.paymentsLoading.set(false);
      },
      error: () => this.paymentsLoading.set(false),
    });
  }

  methodLabel(method: PaymentMethod): string {
    return method === 'Upi' ? 'UPI'
      : method === 'BankTransfer' ? 'Bank transfer'
      : method;
  }

  // ---- record payment drawer ----
  readonly payFor = signal<PlatformTenant | null>(null);
  readonly savingPay = signal(false);
  readonly payMethods: { key: PaymentMethod; label: string }[] = [
    { key: 'Upi', label: 'UPI' },
    { key: 'BankTransfer', label: 'Bank transfer' },
    { key: 'Cash', label: 'Cash' },
    { key: 'Other', label: 'Other' },
  ];
  readonly monthPresets = [1, 3, 6, 12];
  payAmount: number | null = null;
  payMethod: PaymentMethod = 'Upi';
  payMonths = 1;
  payPlan = '';
  payNote = '';
  /** ISO date the money arrived — default today, backdatable. */
  payDate: string | null = null;

  openPay(tenant: PlatformTenant): void {
    this.detailFor.set(null);       // pay drawer replaces the detail drawer
    this.payFor.set(tenant);
    this.payAmount = null;
    this.payMethod = 'Upi';
    this.payMonths = 1;
    this.payPlan = tenant.plan;   // most payments confirm the plan they're on
    this.payNote = '';
    this.payDate = new Date().toISOString().slice(0, 10);
  }

  submitPay(): void {
    const tenant = this.payFor();
    if (!tenant || !this.payAmount || this.payAmount <= 0) return;

    this.savingPay.set(true);
    this.error.set('');
    this.notice.set('');

    this.api.recordPayment(tenant.tenantId, {
      amountRupees: this.payAmount,
      method: this.payMethod,
      periodMonths: this.payMonths,
      paidAt: this.payDate,
      planToApply: this.payPlan || null,
      note: this.payNote.trim() || null,
    }).subscribe({
      next: (updated) => {
        this.tenants.update((list) =>
          list.map((t) => (t.tenantId === updated.tenantId ? updated : t)));
        this.savingPay.set(false);
        this.payFor.set(null);
        this.notice.set(
          `Payment recorded — ${updated.name} is covered until ` +
          `${new Date(updated.paidUntil!).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })}. ` +
          'The clinic has been notified. 🎉');
      },
      error: (err) => {
        this.savingPay.set(false);
        this.error.set(parseApiError(err).message);
      },
    });
  }

  // ---- production email self-test ----
  readonly testingEmail = signal(false);

  sendTestEmail(): void {
    this.testingEmail.set(true);
    this.error.set('');
    this.notice.set('');

    this.api.testEmail().subscribe({
      next: (result) => {
        this.testingEmail.set(false);
        if (result.sent) this.notice.set(`✅ ${result.detail}`);
        else this.error.set(result.detail);
      },
      error: (err) => {
        this.testingEmail.set(false);
        this.error.set(parseApiError(err).message);
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
