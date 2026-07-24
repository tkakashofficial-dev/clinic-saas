import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { InvoicesService } from '../../core/api/invoices.service';
import { ReportsService } from '../../core/api/reports.service';
import { AuthService } from '../../core/auth/auth.service';
import { DuesReport, PatientDues, PracticeOverview } from '../../core/models/api.models';

@Component({
  selector: 'app-reports',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class Reports {
  private readonly api = inject(ReportsService);
  private readonly invoicesApi = inject(InvoicesService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly overview = signal<PracticeOverview | null>(null);
  readonly downloading = signal(false);

  // ---- collections: who owes money ----
  readonly dues = signal<DuesReport | null>(null);
  readonly duesLoading = signal(true);

  /** Bars scaled against the busiest day so the chart always fills nicely. */
  readonly maxPerDay = computed(() =>
    Math.max(1, ...(this.overview()?.appointmentsPerDay.map((d) => d.count) ?? [1])),
  );

  constructor() {
    this.api.getOverview().subscribe({
      next: (overview) => {
        this.overview.set(overview);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.invoicesApi.getDues().subscribe({
      next: (dues) => {
        this.dues.set(dues);
        this.duesLoading.set(false);
      },
      error: () => this.duesLoading.set(false),
    });
  }

  /** How many days the oldest unpaid bill has been waiting. */
  agingDays(row: PatientDues): number {
    return Math.floor((Date.now() - new Date(row.oldestUnpaidAt).getTime()) / 86_400_000);
  }

  /** Free collection nudge — opens WhatsApp with a polite message ready. */
  whatsappNudge(row: PatientDues): string {
    let digits = row.patientPhone.replace(/\D/g, '');
    if (digits.length === 10) digits = `91${digits}`;

    const message =
      `Hi ${row.patientName.split(' ')[0]}! Gentle reminder from ` +
      `${this.auth.clinicName() || 'your clinic'}: there's a pending bill of ` +
      `₹${row.outstandingRupees.toLocaleString('en-IN')} for your treatment. ` +
      `You can pay at your next visit or reply here for details. Thank you! 🙏`;

    return `https://wa.me/${digits}?text=${encodeURIComponent(message)}`;
  }

  barHeight(count: number): string {
    return `${Math.round((count / this.maxPerDay()) * 100)}%`;
  }

  downloadPdf(): void {
    this.downloading.set(true);
    this.api.downloadOverviewPdf().subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'practice-report.pdf';
        link.click();
        URL.revokeObjectURL(url);
        this.downloading.set(false);
      },
      error: () => this.downloading.set(false),
    });
  }

  completionRate(doctor: { total: number; completed: number }): number {
    return doctor.total === 0 ? 0 : Math.round((doctor.completed / doctor.total) * 100);
  }
}
