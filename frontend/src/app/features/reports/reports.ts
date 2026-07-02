import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ReportsService } from '../../core/api/reports.service';
import { PracticeOverview } from '../../core/models/api.models';

@Component({
  selector: 'app-reports',
  imports: [DatePipe],
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class Reports {
  private readonly api = inject(ReportsService);

  readonly loading = signal(true);
  readonly overview = signal<PracticeOverview | null>(null);
  readonly downloading = signal(false);

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
