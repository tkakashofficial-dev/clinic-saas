import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

export const BRAND_NAME = 'Clinora';

interface PricingPlan {
  name: string;
  price: string;
  period: string;
  tagline: string;
  features: string[];
  popular?: boolean;
  cta: string;
}

@Component({
  selector: 'app-landing',
  imports: [RouterLink],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class Landing {
  readonly brand = BRAND_NAME;

  readonly features = [
    {
      icon: 'desk',
      title: 'Front desk that flows',
      text: 'Register a patient, book the visit, check them in as they arrive — reception works in seconds, not screens.',
    },
    {
      icon: 'doctor',
      title: 'Built for the doctor’s day',
      text: 'Your queue, your waiting room, one tap to start a consult. Diagnosis and prescription in the same flow.',
    },
    {
      icon: 'pdf',
      title: 'Beautiful prescriptions',
      text: 'Every prescription becomes a branded PDF with your clinic’s name — print it or send it, instantly.',
    },
    {
      icon: 'roles',
      title: 'Roles done right',
      text: 'Owners, partners, doctors, reception — everyone sees exactly what their job needs. Nothing more.',
    },
    {
      icon: 'shield',
      title: 'Your data stays yours',
      text: 'Each clinic’s data is isolated at the core of the system. No clinic can ever see another’s patients.',
    },
    {
      icon: 'device',
      title: 'Works on anything',
      text: 'Laptop at reception, tablet in the consult room, phone on the go — one calm interface everywhere.',
    },
  ];

  readonly plans: PricingPlan[] = [
    {
      name: 'Solo',
      price: '₹0',
      period: 'forever',
      tagline: 'For a single practicing doctor',
      features: [
        '1 doctor + 1 receptionist',
        'Unlimited patients',
        'Appointments & front-desk flow',
        'Prescriptions with PDF',
      ],
      cta: 'Start free',
    },
    {
      name: 'Clinic',
      price: '₹799',
      period: 'per month',
      tagline: 'For growing clinics with a team',
      popular: true,
      features: [
        'Up to 10 staff members',
        'Everything in Solo',
        'Multiple doctors & partners',
        'Priority support (WhatsApp)',
        'Reminders & reports — coming soon',
      ],
      cta: 'Start 30-day free trial',
    },
    {
      name: 'Growth',
      price: '₹1,499',
      period: 'per month',
      tagline: 'For multi-doctor practices',
      features: [
        'Unlimited staff',
        'Everything in Clinic',
        'Multi-branch — coming soon',
        'Onboarding & data migration help',
        'Dedicated support',
      ],
      cta: 'Start 30-day free trial',
    },
  ];

  readonly faqs = [
    {
      q: 'Is my patient data safe?',
      a: 'Yes. Every clinic’s data is isolated at the database level — it is architecturally impossible for another clinic to read your records. Access requires login, every staff member has their own account, and passwords are stored using industry-standard hashing.',
    },
    {
      q: 'The owner of our clinic is not a doctor. Does that work?',
      a: 'Absolutely. Ownership and being a doctor are separate things here. An owner manages the clinic; only people with the Doctor role appear in the booking list. Partners? Add as many owners as you need.',
    },
    {
      q: 'Can we move from paper or another software?',
      a: 'Yes — you can start fresh in minutes, and on the Growth plan we help migrate your existing patient records.',
    },
    {
      q: 'What if we want to stop?',
      a: 'Cancel anytime, no lock-in, and you can export your data. Your clinic’s records belong to you.',
    },
  ];
}
