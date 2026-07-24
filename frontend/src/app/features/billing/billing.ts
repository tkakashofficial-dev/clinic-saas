import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { parseApiError } from '../../core/api/api-error';
import { BillingService } from '../../core/api/billing.service';
import { AuthService } from '../../core/auth/auth.service';
import { BillingSummary } from '../../core/models/api.models';
import {
  PLAN_PRICING,
  PlanPricing,
  formatInr,
  monthlyEquivalent,
} from '../../core/models/plan-pricing';
import { QrImg } from '../../shared/ui/qr-img';

/** Klivia's own UPI ID — where subscription payments land. */
const KLIVIA_UPI = '6238456205@upi';
const KLIVIA_WHATSAPP = '916238456205';

@Component({
  selector: 'app-billing',
  imports: [DatePipe, QrImg],
  templateUrl: './billing.html',
  styleUrl: './billing.scss',
})
export class Billing {
  private readonly api = inject(BillingService);
  private readonly auth = inject(AuthService);

  readonly planCards = PLAN_PRICING;
  readonly loading = signal(true);
  readonly summary = signal<BillingSummary | null>(null);
  readonly switching = signal<string | null>(null);
  readonly error = signal('');
  readonly success = signal('');

  readonly staffPercent = computed(() => this.usagePercent('staff'));
  readonly doctorPercent = computed(() => this.usagePercent('doctors'));

  constructor() {
    this.load();
  }

  load(): void {
    this.api.getSummary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: (err) => {
        // Never render a silent blank page; say what happened
        this.error.set(parseApiError(err).message + ' Refresh the page to retry.');
        this.loading.set(false);
      },
    });
  }

  choose(plan: PlanPricing): void {
    this.error.set('');
    this.success.set('');
    this.switching.set(plan.key);

    this.api.changePlan(plan.key).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.switching.set(null);
        this.success.set(`You're now on the ${plan.key} plan.`);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.switching.set(null);
      },
    });
  }

  isCurrent(plan: PlanPricing): boolean {
    return this.summary()?.plan === plan.key && !this.summary()?.isInTrial;
  }

  limitLabel(value: number): string {
    return value >= 2000000000 ? 'Unlimited' : String(value);
  }

  priceLabel(amount: number): string {
    return formatInr(amount);
  }

  monthlyEquivalentLabel(amount: number): string {
    return formatInr(monthlyEquivalent(amount));
  }

  private usagePercent(kind: 'staff' | 'doctors'): number {
    const summary = this.summary();
    if (!summary) return 0;
    const used = kind === 'staff' ? summary.staffCount : summary.doctorCount;
    const max = kind === 'staff' ? summary.maxStaff : summary.maxDoctors;
    if (max >= 2000000000) return 8; // unlimited: show a sliver
    return Math.min(100, Math.round((used / max) * 100));
  }

  // ---- pay by UPI (manual flow: scan → pay → WhatsApp the screenshot) ----
  readonly payCycle = signal<'monthly' | 'yearly'>('monthly');

  readonly payPlan = computed(() => {
    const current = this.summary()?.plan;
    return PLAN_PRICING.find((p) => p.key === current) ?? PLAN_PRICING[1];
  });

  readonly payAmount = computed(() => {
    const plan = this.payPlan();
    return this.payCycle() === 'yearly' ? plan.yearlyPrice : plan.monthlyPrice;
  });

  /** upi://pay deep link with plan + clinic in the note for reconciliation. */
  readonly payUpiLink = computed(() =>
    'upi://pay'
    + `?pa=${encodeURIComponent(KLIVIA_UPI)}`
    + '&pn=Klivia'
    + `&am=${this.payAmount()}`
    + '&cu=INR'
    + `&tn=${encodeURIComponent(`Klivia ${this.payPlan().key} ${this.payCycle()} — ${this.auth.clinicName()}`)}`);

  readonly payConfirmLink = computed(() => {
    const message =
      `Hi Klivia! I've paid ${formatInr(this.payAmount())} for the ` +
      `${this.payPlan().key} plan (${this.payCycle()}) — clinic: ` +
      `${this.auth.clinicName()}. Screenshot attached.`;
    return `https://wa.me/${KLIVIA_WHATSAPP}?text=${encodeURIComponent(message)}`;
  });
}
