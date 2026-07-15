export type PlanKey = 'Solo' | 'Clinic' | 'Growth';

export interface PlanPricing {
  key: PlanKey;
  /** True monthly price — the anchor customers compare against eka/Practo. */
  monthlyPrice: number;
  /** 10× monthly: "pay yearly, get 2 months free" — cash upfront, zero churn. */
  yearlyPrice: number;
  scale: string;
  tagline: string;
  features: string[];
  popular?: boolean;
}

// Pricing psychology (decided 2026-07-14):
// - MONTHLY is the anchor (₹999/1,999/3,999) — still ~half of eka.care while
//   giving away what they sell as add-ons. Yearly = 10× monthly, sold as
//   "2 months free": upfront cash, a year of zero churn, healthy margin.
// - Ladder that sells itself (revised 2026-07-15): Solo hits its 1-doctor
//   wall AND has no pharmacy/inventory (Clinic's headline unlock, API-
//   enforced 402); a second BRANCH requires Growth (also API-enforced).
//   Locked features stay VISIBLE in-app as styled upgrade pitches —
//   invisible features never sell.
export const PLAN_PRICING: PlanPricing[] = [
  {
    key: 'Solo',
    monthlyPrice: 999,
    yearlyPrice: 9990,
    scale: '1 doctor - 1 location',
    tagline: 'For independent doctors starting out',
    features: [
      'Unlimited patients & appointments',
      'Front-desk flow with check-in & waiting room',
      'Consultations, vitals & designer PDF prescriptions',
      'Printable intake forms — dental & general',
      'Works on laptop, tablet & phone',
    ],
  },
  {
    key: 'Clinic',
    monthlyPrice: 1999,
    yearlyPrice: 19990,
    scale: '2-5 doctors - 1 location',
    tagline: 'For clinics with a real team',
    popular: true,
    features: [
      'Everything in Solo',
      'Pharmacy & inventory with low-stock alerts',
      'Multiple doctors, partners & reception',
      'Visiting doctors — one account, many clinics',
      'Appointment reminders & notifications',
      'Practice analytics, doctor reports & PDF export',
      'Priority support on WhatsApp',
    ],
  },
  {
    key: 'Growth',
    monthlyPrice: 3999,
    yearlyPrice: 39990,
    scale: '6+ doctors - multi-location',
    tagline: 'For practices opening branches',
    features: [
      'Everything in Clinic',
      'Unlimited doctors & staff',
      'Multiple clinics under one login — Growth exclusive',
      'Consolidated view of every branch',
      'Onboarding + data migration help',
      'Early access to new features (form builder, WhatsApp)',
    ],
  },
];

export function formatInr(amount: number): string {
  return `Rs ${amount.toLocaleString('en-IN')}`;
}

/** What a yearly subscriber effectively pays per month (the "save" hook). */
export function monthlyEquivalent(yearlyPrice: number): number {
  return Math.floor(yearlyPrice / 12);
}
