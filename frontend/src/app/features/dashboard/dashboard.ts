import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AppointmentsService } from '../../core/api/appointments.service';
import { PatientsService } from '../../core/api/patients.service';
import { StaffService } from '../../core/api/staff.service';
import { AuthService } from '../../core/auth/auth.service';
import { AppointmentDto } from '../../core/models/api.models';

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

  readonly loading = signal(true);
  readonly totalPatients = signal(0);
  readonly todayCount = signal(0);
  readonly staffCount = signal<number | null>(null);
  readonly todaysAppointments = signal<AppointmentDto[]>([]);
  readonly today = new Date();

  readonly greeting = getGreeting();

  constructor() {
    const todayIso = this.today.toISOString().split('T')[0];

    // totalCount from pageSize=1 requests — stats without extra endpoints
    forkJoin({
      patients: this.patientsApi.getAll('', 1, 1),
      todays: this.appointmentsApi.getAll({ date: todayIso, page: 1, pageSize: 8 }),
    }).subscribe({
      next: ({ patients, todays }) => {
        this.totalPatients.set(patients.totalCount);
        this.todayCount.set(todays.totalCount);
        this.todaysAppointments.set(todays.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    if (this.auth.hasRole('Admin')) {
      this.staffApi.getAll(1, 1).subscribe({
        next: (staff) => this.staffCount.set(staff.totalCount),
      });
    }
  }
}

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 18) return 'Good afternoon';
  return 'Good evening';
}
