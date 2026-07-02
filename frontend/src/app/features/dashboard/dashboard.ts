import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { AppointmentsService } from '../../core/api/appointments.service';
import { PatientsService } from '../../core/api/patients.service';
import { ReportsService } from '../../core/api/reports.service';
import { StaffService } from '../../core/api/staff.service';
import { AuthService } from '../../core/auth/auth.service';
import { AppointmentDto, PracticeOverview } from '../../core/models/api.models';

/**
 * The dashboard adapts to WHO is looking at it:
 * - Receptionist -> front desk: today's arrivals + waiting room + check-in
 * - Doctor       -> my day: my queue, who's waiting for me
 * - Admin        -> the clinic: patients, schedule, team
 */
@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  readonly auth = inject(AuthService);
  private readonly patientsApi = inject(PatientsService);
  private readonly appointmentsApi = inject(AppointmentsService);
  private readonly staffApi = inject(StaffService);
  private readonly reportsApi = inject(ReportsService);

  readonly loading = signal(true);
  readonly overview = signal<PracticeOverview | null>(null);

  readonly maxPerDay = computed(() =>
    Math.max(1, ...(this.overview()?.appointmentsPerDay.map((d) => d.count) ?? [1])),
  );

  barHeight(count: number): string {
    return `${Math.round((count / this.maxPerDay()) * 100)}%`;
  }
  readonly totalPatients = signal(0);
  readonly todayCount = signal(0);
  readonly waitingCount = signal(0);
  readonly completedToday = signal(0);
  readonly staffCount = signal(0);
  readonly todaysAppointments = signal<AppointmentDto[]>([]);
  readonly today = new Date();

  readonly greeting = getGreeting();
  readonly isDoctor = computed(() => this.auth.role() === 'Doctor');
  readonly canCheckIn = computed(() => this.auth.hasRole('Admin', 'Receptionist'));

  readonly subtitle = computed(() => {
    switch (this.auth.role()) {
      case 'Doctor': return 'Your day at a glance';
      case 'Receptionist': return 'Front desk overview';
      default: return 'Clinic overview';
    }
  });

  constructor() {
    this.load();

    // Trend chart for owners — beauty AND signal
    if (this.auth.hasRole('Admin')) {
      this.reportsApi.getOverview().subscribe({
        next: (overview) => this.overview.set(overview),
      });
    }
  }

  load(): void {
    this.loading.set(true);
    const todayIso = this.today.toISOString().split('T')[0];
    // Doctors see THEIR day; front desk and admin see the whole clinic
    const doctorFilter = this.isDoctor()
      ? (this.auth.session()?.tenantUserId ?? undefined)
      : undefined;

    forkJoin({
      todays: this.appointmentsApi.getAll({
        date: todayIso, doctorTenantUserId: doctorFilter, page: 1, pageSize: 8,
      }),
      waiting: this.appointmentsApi.getAll({
        date: todayIso, status: 'CheckedIn', doctorTenantUserId: doctorFilter, page: 1, pageSize: 1,
      }),
      completed: this.isDoctor()
        ? this.appointmentsApi.getAll({
            date: todayIso, status: 'Completed', doctorTenantUserId: doctorFilter, page: 1, pageSize: 1,
          })
        : of(null),
      patients: this.isDoctor() ? of(null) : this.patientsApi.getAll('', 1, 1),
      staff: this.auth.hasRole('Admin') ? this.staffApi.getAll(1, 1) : of(null),
    }).subscribe({
      next: ({ todays, waiting, completed, patients, staff }) => {
        this.todaysAppointments.set(todays.items);
        this.todayCount.set(todays.totalCount);
        this.waitingCount.set(waiting.totalCount);
        if (completed) this.completedToday.set(completed.totalCount);
        if (patients) this.totalPatients.set(patients.totalCount);
        if (staff) this.staffCount.set(staff.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  checkIn(appointment: AppointmentDto): void {
    this.appointmentsApi.updateStatus(appointment.id, 'CheckedIn').subscribe({
      next: () => this.load(),
    });
  }

  statusLabel(status: string): string {
    return status === 'CheckedIn' ? 'Waiting' : status === 'InProgress' ? 'In progress' : status;
  }
}

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}
