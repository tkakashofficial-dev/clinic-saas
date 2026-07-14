export type PlanKey = 'Solo' | 'Clinic' | 'Growth';

export interface PlanPricing {
  key: PlanKey;
  yearlyPrice: number;
  scale: string;
  tagline: string;
  features: string[];
  popular?: boolean;
}

// Positioning: EVERY plan gets every feature — tiers price team size, not
// crippled functionality. That is the anti-eka.care pitch: no ₹9,999/yr
// add-ons for forms, no per-module pricing. Simple to sell, simple to buy.
export const PLAN_PRICING: PlanPricing[] = [
  {
    key: 'Solo',
    yearlyPrice: 9599,
    scale: '1 doctor - 1 location',
    tagline: 'For independent doctors starting out',
    features: [
      'Unlimited patients & appointments',
      'Front-desk flow with check-in & waiting room',
      'Consultations, vitals & designer PDF prescriptions',
      'Printable intake forms — dental & general',
      'Pharmacy & inventory with low-stock alerts',
      'Works on laptop, tablet & phone',
    ],
  },
  {
    key: 'Clinic',
    yearlyPrice: 17999,
    scale: '2-5 doctors - 1 location',
    tagline: 'For clinics with a real team',
    popular: true,
    features: [
      'Everything in Solo',
      'Multiple doctors, partners & reception',
      'Visiting doctors — one account, many clinics',
      'Appointment reminders & notifications',
      'Practice analytics, doctor reports & PDF export',
      'Priority support on WhatsApp',
    ],
  },
  {
    key: 'Growth',
    yearlyPrice: 35999,
    scale: '6+ doctors - multi-location',
    tagline: 'For practices that keep growing',
    features: [
      'Everything in Clinic',
      'Unlimited doctors & staff',
      'Multiple clinics, one login (switcher)',
      'Onboarding + data migration help',
      'Early access to new features (form builder, WhatsApp)',
    ],
  },
];

export function formatInr(amount: number): string {
  return `Rs ${amount.toLocaleString('en-IN')}`;
}

export function monthlyEquivalent(yearlyPrice: number): number {
  return Math.floor(yearlyPrice / 12);
}
