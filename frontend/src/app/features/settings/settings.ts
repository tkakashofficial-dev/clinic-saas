import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { SettingsService } from '../../core/api/settings.service';
import { IntakeTemplate } from '../../core/models/api.models';
import { QrImg } from '../../shared/ui/qr-img';

interface TemplateOption {
  key: IntakeTemplate;
  emoji: string;
  title: string;
  sections: string[];
}

/**
 * Clinic settings (Admin): the clinic's letterhead — name, phone and address
 * printed on every prescription and intake form — and which intake-form
 * template the clinic uses by default. Admin manages templates; doctors and
 * reception just print them.
 */
@Component({
  selector: 'app-settings',
  imports: [ReactiveFormsModule, RouterLink, QrImg],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  private readonly api = inject(SettingsService);
  private readonly fb = inject(FormBuilder);

  readonly templates: TemplateOption[] = [
    {
      key: 'dental',
      emoji: '🦷',
      title: 'Dental clinic',
      sections: [
        'Chief complaint & disease checklist',
        'Dental & medical history',
        'Oral health status',
        'Extra / intra oral examination',
        'Ortho findings & treatment table',
        'Informed consent + signatures',
      ],
    },
    {
      key: 'general',
      emoji: '🩺',
      title: 'General clinic',
      sections: [
        'Vitals strip (BP, pulse, temp, SpO₂…)',
        'Chief complaint & disease checklist',
        'Medical, surgical & family history',
        'General & systemic examination',
        'Findings & treatment table',
        'Informed consent + signatures',
      ],
    },
  ];

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly selectedTemplate = signal<IntakeTemplate>('dental');

  // ---- online booking + UPI ----
  readonly bookingEnabled = signal(false);
  readonly slug = signal<string | null>(null);
  readonly linkCopied = signal(false);

  /** The shareable patient-facing URL — WhatsApp status, Google profile… */
  readonly bookingUrl = computed(() =>
    this.slug() ? `${location.origin}/book/${this.slug()}` : null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['' as string | null],
    address: ['' as string | null],
    upiId: ['' as string | null],
  });

  constructor() {
    this.api.get().subscribe({
      next: (settings) => {
        this.form.patchValue({
          name: settings.name,
          phone: settings.phone ?? '',
          address: settings.address ?? '',
          upiId: settings.upiId ?? '',
        });
        this.selectedTemplate.set(settings.defaultIntakeTemplate);
        this.bookingEnabled.set(settings.publicBookingEnabled);
        this.slug.set(settings.slug);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(parseApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  copyBookingLink(): void {
    const url = this.bookingUrl();
    if (!url) return;
    navigator.clipboard.writeText(url).then(() => {
      this.linkCopied.set(true);
      setTimeout(() => this.linkCopied.set(false), 2000);
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');

    const value = this.form.getRawValue();
    this.api.update({
      name: value.name,
      phone: value.phone || null,
      address: value.address || null,
      defaultIntakeTemplate: this.selectedTemplate(),
      upiId: value.upiId || null,
      publicBookingEnabled: this.bookingEnabled(),
      // slug is server-minted and write-once; sent back only for the signal
      slug: this.slug(),
    }).subscribe({
      next: (settings) => {
        this.saving.set(false);
        this.slug.set(settings.slug);
        this.notice.set('Settings saved — new PDFs use the updated details.');
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(parseApiError(err).message);
      },
    });
  }
}
