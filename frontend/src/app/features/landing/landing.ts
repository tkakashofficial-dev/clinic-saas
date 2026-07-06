import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  PLAN_PRICING,
  PlanPricing,
  formatInr,
  monthlyEquivalent,
} from '../../core/models/plan-pricing';

export const BRAND_NAME = 'Klivia';
export const WHATSAPP_LINK =
  'https://wa.me/916238456205?text=' +
  encodeURIComponent('Hi Klivia! I want a demo for my clinic.');

@Component({
  selector: 'app-landing',
  imports: [RouterLink],
  templateUrl: './landing.html',
  styleUrl: './landing.scss',
})
export class Landing {
  readonly brand = BRAND_NAME;
  readonly whatsapp = WHATSAPP_LINK;

  readonly features = [
    {
      icon: 'desk',
      title: 'Front desk that flows',
      text: 'Register a patient, book the visit, check them in as they arrive - reception works in seconds, not screens.',
    },
    {
      icon: 'doctor',
      title: 'Built for the doctor\'s day',
      text: 'Your queue, your waiting room, one tap to start a consult. Diagnosis and prescription in the same flow.',
    },
    {
      icon: 'pdf',
      title: 'Beautiful prescriptions',
      text: 'Every prescription becomes a branded PDF with your clinic\'s name - print it or send it, instantly.',
    },
    {
      icon: 'roles',
      title: 'Roles done right',
      text: 'Owners, partners, doctors, reception - everyone sees exactly what their job needs. Nothing more.',
    },
    {
      icon: 'shield',
      title: 'Your data stays yours',
      text: 'Each clinic\'s data is isolated at the core of the system. No clinic can ever see another\'s patients.',
    },
    {
      icon: 'device',
      title: 'Works on anything',
      text: 'Laptop at reception, tablet in the consult room, phone on the go - one calm interface everywhere.',
    },
  ];

  readonly plans = PLAN_PRICING.map((plan) => ({
    ...plan,
    name: plan.key,
    cta: plan.key === 'Growth' ? 'Talk to us' : 'Start free trial',
  }));

  priceOf(plan: PlanPricing): string {
    return formatInr(plan.yearlyPrice);
  }

  monthlyEquivalentOf(plan: PlanPricing): string {
    return formatInr(monthlyEquivalent(plan.yearlyPrice));
  }

  readonly faqs = [
    {
      q: 'Is my patient data safe?',
      a: 'Yes. Every clinic\'s data is isolated at the database level - it is architecturally impossible for another clinic to read your records. Access requires login, every staff member has their own account, and passwords are stored using industry-standard hashing.',
    },
    {
      q: 'The owner of our clinic is not a doctor. Does that work?',
      a: 'Absolutely. Ownership and being a doctor are separate things here. An owner manages the clinic; only people with the Doctor role appear in the booking list. Partners? Add as many owners as you need.',
    },
    {
      q: 'Can we move from paper or another software?',
      a: 'Yes - you can start fresh in minutes, and on the Growth plan we help migrate your existing patient records.',
    },
    {
      q: 'What if we want to stop?',
      a: 'Cancel anytime, no lock-in, and you can export your data. Your clinic\'s records belong to you.',
    },
  ];
}
