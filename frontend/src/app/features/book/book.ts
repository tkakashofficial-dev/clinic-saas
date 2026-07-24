import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { parseApiError } from '../../core/api/api-error';
import { PublicBookingService } from '../../core/api/public-booking.service';
import { PublicBookingResult, PublicClinic } from '../../core/models/api.models';

interface DayOption {
  iso: string;        // yyyy-MM-dd (local)
  label: string;      // "Mon"
  dayNum: number;     // 22
  isToday: boolean;
}

/**
 * The patient-facing booking page (/book/:slug) — no login, no app install,
 * works from a WhatsApp status tap or a QR at the reception desk. Every
 * booking lands as a Scheduled appointment + notification inside Klivia.
 */
@Component({
  selector: 'app-book',
  imports: [DatePipe, FormsModule],
  templateUrl: './book.html',
  styleUrl: './book.scss',
})
export class Book {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PublicBookingService);

  private readonly slug = this.route.snapshot.paramMap.get('slug') ?? '';

  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly clinic = signal<PublicClinic | null>(null);

  // ---- form state ----
  readonly doctorId = signal('');
  readonly dateIso = signal('');
  readonly time = signal('');
  name = '';
  phone = '';
  note = '';
  /** Honeypot — humans never see it, bots fill it. */
  website = '';

  readonly submitting = signal(false);
  readonly error = signal('');
  readonly done = signal<PublicBookingResult | null>(null);

  /** Next 14 days — nobody plans a clinic visit further out on a phone. */
  readonly days: DayOption[] = Array.from({ length: 14 }, (_, i) => {
    const d = new Date();
    d.setDate(d.getDate() + i);
    return {
      iso: toLocalIso(d),
      label: i === 0 ? 'Today' : d.toLocaleDateString('en-IN', { weekday: 'short' }),
      dayNum: d.getDate(),
      isToday: i === 0,
    };
  });

  readonly slotGroups = [
    { label: 'Morning', slots: buildSlots(9, 13) },
    { label: 'Afternoon', slots: buildSlots(13, 17) },
    { label: 'Evening', slots: buildSlots(17, 21) },
  ];

  /** Past slots disappear when "Today" is picked — no dead choices. */
  readonly visibleSlotGroups = computed(() => {
    if (this.dateIso() !== this.days[0].iso) return this.slotGroups;
    const now = new Date();
    const cutoff = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
    return this.slotGroups
      .map((g) => ({ ...g, slots: g.slots.filter((s) => s > cutoff) }))
      .filter((g) => g.slots.length > 0);
  });

  readonly canSubmit = computed(() =>
    !!this.doctorId() && !!this.dateIso() && !!this.time());

  constructor() {
    this.api.getClinic(this.slug).subscribe({
      next: (clinic) => {
        this.clinic.set(clinic);
        if (clinic.doctors.length === 1) this.doctorId.set(clinic.doctors[0].id);
        this.dateIso.set(this.days[0].iso);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  initials(name: string): string {
    return name.replace(/^Dr\.\s*/, '').split(' ').filter(Boolean)
      .map((w) => w[0]).slice(0, 2).join('').toUpperCase();
  }

  clinicInitials(): string {
    return (this.clinic()?.name ?? '')
      .split(' ').filter(Boolean).map((w) => w[0]).slice(0, 2).join('').toUpperCase();
  }

  submit(): void {
    const name = this.name.trim();
    const phone = this.phone.trim();
    if (name.length < 2) { this.error.set('Please enter your full name.'); return; }
    if (!/^\+?[0-9 ()\-]{7,20}$/.test(phone)) {
      this.error.set('Please enter a valid phone number.');
      return;
    }
    if (!this.canSubmit()) { this.error.set('Pick a doctor, a day and a time.'); return; }

    // Local date+time → UTC instant (the patient's phone is in IST)
    const appointmentAt = new Date(`${this.dateIso()}T${this.time()}:00`).toISOString();

    this.submitting.set(true);
    this.error.set('');
    this.api.book(this.slug, {
      doctorId: this.doctorId(),
      appointmentAt,
      patientName: name,
      phone,
      note: this.note.trim() || null,
      website: this.website,
    }).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.done.set(result);
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(parseApiError(err).message);
      },
    });
  }
}

function toLocalIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function buildSlots(fromHour: number, toHour: number): string[] {
  const slots: string[] = [];
  for (let h = fromHour; h < toHour; h++) {
    slots.push(`${String(h).padStart(2, '0')}:00`, `${String(h).padStart(2, '0')}:30`);
  }
  return slots;
}
