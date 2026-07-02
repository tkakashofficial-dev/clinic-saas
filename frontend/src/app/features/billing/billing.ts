import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { parseApiError } from '../../core/api/api-error';
import { BillingService } from '../../core/api/billing.service';
import { BillingSummary } from '../../core/models/api.models';

interface PlanCard {
  key: 'Solo' | 'Clinic' | 'Growth';
  price: number;
  scale: string;
  features: string[];
  popular?: boolean;
}

const PLAN_CARDS: PlanCard[] = [
  {
    key: 'Solo',
    price: 499,
    scale: '1 doctor · 2 staff total',
    features: ['Unlimited patients', 'Appointments & check-in', 'Prescriptions with PDF'],
  },
  {
    key: 'Clinic',
    price: 1499,
    scale: '5 doctors · 10 staff total',
    popular: true,
    features: ['Everything in Solo', 'Team waiting-room queue', 'Reminders & notifications', 'Practice reports'],
  },
  {
    key: 'Growth',
    price: 2999,
    scale: 'Unlimited doctors & staff',
    features: ['Everything in Clinic', 'Multiple clinics, one login', 'Priority support'],
  },
];

@Component({
  selector: 'app-billing',
  imports: [DatePipe],
  templateUrl: './billing.html',
  styleUrl: './billing.scss',
})
export class Billing {
  private readonly api = inject(BillingService);

  readonly planCards = PLAN_CARDS;
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
        // Never render a silent blank page — say what happened
        this.error.set(parseApiError(err).message + ' Refresh the page to retry.');
        this.loading.set(false);
      },
    });
  }

  choose(plan: PlanCard): void {
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

  isCurrent(plan: PlanCard): boolean {
    return this.summary()?.plan === plan.key && !this.summary()?.isInTrial;
  }

  limitLabel(value: number): string {
    return value >= 2_000_000_000 ? '∞' : String(value);
  }

  private usagePercent(kind: 'staff' | 'doctors'): number {
    const summary = this.summary();
    if (!summary) return 0;
    const used = kind === 'staff' ? summary.staffCount : summary.doctorCount;
    const max = kind === 'staff' ? summary.maxStaff : summary.maxDoctors;
    if (max >= 2_000_000_000) return 8; // unlimited: show a sliver
    return Math.min(100, Math.round((used / max) * 100));
  }
}
