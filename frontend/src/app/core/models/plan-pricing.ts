export type PlanKey = 'Solo' | 'Clinic' | 'Growth';

export interface PlanPricing {
  key: PlanKey;
  yearlyPrice: number;
  scale: string;
  tagline: string;
  features: string[];
  popular?: boolean;
}

export const PLAN_PRICING: PlanPricing[] = [
  {
    key: 'Solo',
    yearlyPrice: 9999,
    scale: '1 doctor - 1 location',
    tagline: 'For independent doctors starting out',
    features: [
      'Unlimited patients & appointments',
      'Front-desk flow with check-in',
      'Consultations & prescriptions (PDF)',
      'Works on laptop, tablet & phone',
    ],
  },
  {
    key: 'Clinic',
    yearlyPrice: 19999,
    scale: '2-5 doctors - 1 location',
    tagline: 'For clinics with a real team',
    popular: true,
    features: [
      'Everything in Solo',
      'Multiple doctors, partners & reception',
      'Waiting-room queue for the whole team',
      'Appointment reminders & notifications',
      'Practice analytics & doctor reports',
      'Priority support on WhatsApp',
    ],
  },
  {
    key: 'Growth',
    yearlyPrice: 39999,
    scale: '6+ doctors - multi-location',
    tagline: 'For practices that keep growing',
    features: [
      'Everything in Clinic',
      'Unlimited doctors & staff',
      'Multiple clinics, one login (switcher)',
      'Onboarding + data migration help',
      'Pharmacy & inventory - coming soon',
    ],
  },
];

export function formatInr(amount: number): string {
  return `Rs ${amount.toLocaleString('en-IN')}`;
}

export function monthlyEquivalent(yearlyPrice: number): number {
  return Math.round(yearlyPrice / 12);
}
