import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { parseApiError } from '../../core/api/api-error';
import { AppointmentsService } from '../../core/api/appointments.service';
import { InventoryService } from '../../core/api/inventory.service';
import { PatientsService } from '../../core/api/patients.service';
import { StaffService } from '../../core/api/staff.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  AppointmentDto,
  ConsultationDto,
  PagedResult,
  PatientDto,
  StaffDto,
} from '../../core/models/api.models';
import { CalendarPop } from '../../shared/ui/calendar-pop';
import { DateField } from '../../shared/ui/date-field';
import { Select, SelectOption } from '../../shared/ui/select';

const STATUS_FILTERS = ['All', 'Scheduled', 'CheckedIn', 'InProgress', 'Completed', 'Cancelled'] as const;

const STATUS_LABELS: Record<string, string> = {
  CheckedIn: 'Waiting',
  InProgress: 'In progress',
};

@Component({
  selector: 'app-appointments',
  imports: [DatePipe, ReactiveFormsModule, Select, CalendarPop, DateField],
  templateUrl: './appointments.html',
  styleUrl: './appointments.scss',
})
export class Appointments {
  readonly auth = inject(AuthService);
  private readonly api = inject(AppointmentsService);
  private readonly patientsApi = inject(PatientsService);
  private readonly staffApi = inject(StaffService);
  private readonly inventoryApi = inject(InventoryService);
  private readonly fb = inject(FormBuilder);

  /**
   * Guided prescription entry — Indian clinic conventions:
   * dose pattern morning-noon-night (1-0-1) and food timing. One tap fills
   * the field; free typing still works for anything unusual.
   */
  readonly freqPresets = ['1-0-0', '1-0-1', '1-1-1', '0-0-1'];
  readonly instructionPresets = ['After food', 'Before food', 'At bedtime', 'With warm water'];

  /** Medicine-name suggestions from the clinic's own inventory. */
  readonly medicineSuggestions = signal<string[]>([]);
  private readonly medicineQuery$ = new Subject<string>();

  readonly statusFilters = STATUS_FILTERS;

  // ---- list state ----
  readonly loading = signal(true);
  readonly result = signal<PagedResult<AppointmentDto> | null>(null);
  readonly date = signal(todayIso());
  /** Branded calendar popover on the day navigator. */
  readonly calOpen = signal(false);
  readonly status = signal<(typeof STATUS_FILTERS)[number]>('All');
  readonly onlyMine = signal(this.auth.hasRole('Doctor'));
  readonly page = signal(1);

  // ---- booking drawer ----
  readonly bookOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly doctors = signal<StaffDto[]>([]);
  readonly patientResults = signal<PatientDto[]>([]);
  readonly selectedPatient = signal<PatientDto | null>(null);
  private readonly patientSearch$ = new Subject<string>();

  readonly bookForm = this.fb.nonNullable.group({
    doctorTenantUserId: ['', Validators.required],
    date: [todayIso(), Validators.required],
    time: ['09:00', Validators.required],
    notes: [''],
  });

  // ---- consultation drawers ----
  readonly recordFor = signal<AppointmentDto | null>(null);
  readonly viewFor = signal<AppointmentDto | null>(null);
  readonly consultation = signal<ConsultationDto | null>(null);
  readonly consultationLoading = signal(false);
  readonly withPrescription = signal(false);
  readonly downloadingPdf = signal(false);

  readonly consultForm = this.fb.nonNullable.group({
    diagnosis: ['', Validators.required],
    treatmentNotes: [''],
    bloodPressure: [''],
    pulseBpm: [''],
    temperatureCelsius: [''],
    weightKg: [''],
    prescriptionNotes: [''],
    items: this.fb.array([this.createItemGroup()]),
  });

  get items() {
    return this.consultForm.controls.items;
  }

  readonly canManage = computed(() => this.auth.hasRole('Admin', 'Receptionist'));
  readonly canConsult = computed(() => this.auth.hasRole('Admin', 'Doctor'));

  readonly doctorOptions = computed<SelectOption[]>(() =>
    this.doctors().map((doctor) => ({
      value: doctor.id,
      label: doctor.fullName,
      sublabel: doctor.roles.join(' · '),
    })),
  );

  statusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  /**
   * Free WhatsApp reminder: opens WhatsApp with the message pre-written —
   * the receptionist just hits send. Zero cost (it's the clinic's own
   * WhatsApp), unlike the automated Business API.
   */
  whatsappReminder(appointment: AppointmentDto): string {
    // 10-digit Indian numbers need the country code for wa.me
    let digits = appointment.patientPhone.replace(/\D/g, '');
    if (digits.length === 10) digits = `91${digits}`;

    const when = new Date(appointment.appointmentDate);
    const message =
      `Hi ${appointment.patientName.split(' ')[0]}! Reminder from ` +
      `${this.auth.clinicName() || 'your clinic'}: your appointment with ` +
      `${appointment.doctorName} is on ${when.toLocaleDateString('en-IN', {
        weekday: 'short', day: 'numeric', month: 'short',
      })} at ${when.toLocaleTimeString('en-IN', {
        hour: '2-digit', minute: '2-digit',
      })}. Reply here if you need to reschedule. 🙏`;

    return `https://wa.me/${digits}?text=${encodeURIComponent(message)}`;
  }

  constructor() {
    this.patientSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => this.patientsApi.getAll(term, 1, 8)),
        takeUntilDestroyed(),
      )
      .subscribe((result) => this.patientResults.set(result.items));

    this.medicineQuery$
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => this.inventoryApi.suggestMedicines(term)),
        takeUntilDestroyed(),
      )
      .subscribe((names) => this.medicineSuggestions.set(names));

    this.load();
  }

  onMedicineInput(value: string): void {
    if (value.trim().length >= 2) this.medicineQuery$.next(value.trim());
  }

  /** Quick-chip fill for a prescription row (frequency / instructions). */
  setItemPreset(index: number, control: 'frequency' | 'instructions', value: string): void {
    this.items.at(index).patchValue({ [control]: value });
  }

  // ---------- list ----------

  load(): void {
    this.loading.set(true);
    this.api
      .getAll({
        date: this.date() || undefined,
        status: this.status() === 'All' ? undefined : this.status(),
        doctorTenantUserId: this.onlyMine()
          ? (this.auth.session()?.tenantUserId ?? undefined)
          : undefined,
        page: this.page(),
        pageSize: 10,
      })
      .subscribe({
        next: (result) => {
          this.result.set(result);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  setDate(value: string): void {
    this.date.set(value);
    this.page.set(1);
    this.load();
  }

  /** Day navigator: ‹ yesterday · today · tomorrow › */
  shiftDate(days: number): void {
    const current = this.date() ? new Date(this.date()) : new Date();
    current.setDate(current.getDate() + days);
    this.setDate(current.toISOString().split('T')[0]);
  }

  goToday(): void {
    this.setDate(todayIso());
  }

  isToday(): boolean {
    return this.date() === todayIso();
  }

  setStatus(value: (typeof STATUS_FILTERS)[number]): void {
    this.status.set(value);
    this.page.set(1);
    this.load();
  }

  toggleMine(): void {
    this.onlyMine.update((v) => !v);
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  updateStatus(appointment: AppointmentDto, status: string): void {
    this.api.updateStatus(appointment.id, status).subscribe({ next: () => this.load() });
  }

  // ---------- booking ----------

  openBook(): void {
    this.bookForm.reset({ doctorTenantUserId: '', date: this.date() || todayIso(), time: '09:00', notes: '' });
    this.selectedPatient.set(null);
    this.loadRecentPatients();   // dropdown works without remembering names
    this.formError.set('');
    this.bookOpen.set(true);

    if (this.doctors().length === 0) {
      this.staffApi.getAll().subscribe({
        next: (staff) =>
          // Only people who actually hold the Doctor role are bookable —
          // an investor-admin never appears here
          this.doctors.set(staff.items.filter((s) => s.roles.includes('Doctor'))),
      });
    }
  }

  /** Empty field shows the most recent patients; typing filters everyone. */
  private loadRecentPatients(): void {
    this.patientsApi.getAll('', 1, 8).subscribe({
      next: (result) => this.patientResults.set(result.items),
      error: () => {},
    });
  }

  searchPatients(term: string): void {
    if (term.trim().length === 0) this.loadRecentPatients();
    else this.patientSearch$.next(term);
  }

  pickPatient(patient: PatientDto): void {
    this.selectedPatient.set(patient);
    this.patientResults.set([]);
  }

  book(): void {
    if (!this.selectedPatient()) {
      this.formError.set('Select a patient first.');
      return;
    }
    if (this.bookForm.invalid) {
      this.bookForm.markAllAsTouched();
      return;
    }

    const value = this.bookForm.getRawValue();
    this.saving.set(true);
    this.formError.set('');

    this.api
      .create({
        patientId: this.selectedPatient()!.id,
        doctorTenantUserId: value.doctorTenantUserId,
        appointmentDate: new Date(`${value.date}T${value.time}`).toISOString(),
        notes: value.notes || null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.bookOpen.set(false);
          this.load();
        },
        error: (err) => {
          this.formError.set(parseApiError(err).message);
          this.saving.set(false);
        },
      });
  }

  // ---------- consultation: record ----------

  openRecord(appointment: AppointmentDto): void {
    this.consultForm.reset({
      diagnosis: '', treatmentNotes: '', prescriptionNotes: '',
      bloodPressure: '', pulseBpm: '', temperatureCelsius: '', weightKg: '',
    });
    this.items.clear();
    this.items.push(this.createItemGroup());
    this.withPrescription.set(false);
    this.formError.set('');
    this.recordFor.set(appointment);
  }

  addItem(): void {
    this.items.push(this.createItemGroup());
  }

  removeItem(index: number): void {
    if (this.items.length > 1) this.items.removeAt(index);
  }

  saveConsultation(): void {
    if (this.consultForm.controls.diagnosis.invalid) {
      this.consultForm.controls.diagnosis.markAsTouched();
      return;
    }

    const value = this.consultForm.getRawValue();
    this.saving.set(true);
    this.formError.set('');

    this.api
      .recordConsultation(this.recordFor()!.id, {
        diagnosis: value.diagnosis,
        treatmentNotes: value.treatmentNotes || null,
        bloodPressure: value.bloodPressure || null,
        pulseBpm: value.pulseBpm ? Number(value.pulseBpm) : null,
        temperatureCelsius: value.temperatureCelsius ? Number(value.temperatureCelsius) : null,
        weightKg: value.weightKg ? Number(value.weightKg) : null,
        prescription: this.withPrescription()
          ? {
              notes: value.prescriptionNotes || null,
              items: value.items
                .filter((item) => item.medicineName.trim())
                .map((item) => ({
                  medicineName: item.medicineName,
                  dosage: item.dosage || null,
                  frequency: item.frequency || null,
                  durationDays: item.durationDays ? Number(item.durationDays) : null,
                  instructions: item.instructions || null,
                })),
            }
          : null,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.recordFor.set(null);
          this.load();
        },
        error: (err) => {
          this.formError.set(parseApiError(err).message);
          this.saving.set(false);
        },
      });
  }

  // ---------- consultation: view ----------

  openView(appointment: AppointmentDto): void {
    this.viewFor.set(appointment);
    this.consultation.set(null);
    this.consultationLoading.set(true);
    this.api.getConsultation(appointment.id).subscribe({
      next: (dto) => {
        this.consultation.set(dto);
        this.consultationLoading.set(false);
      },
      error: () => this.consultationLoading.set(false),
    });
  }

  downloadPdf(): void {
    const prescription = this.consultation()?.prescription;
    if (!prescription) return;

    this.downloadingPdf.set(true);
    this.api.downloadPrescriptionPdf(prescription.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `prescription-${this.viewFor()?.patientName ?? 'patient'}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
        this.downloadingPdf.set(false);
      },
      error: () => this.downloadingPdf.set(false),
    });
  }

  closeDrawers(): void {
    this.bookOpen.set(false);
    this.recordFor.set(null);
    this.viewFor.set(null);
  }

  private createItemGroup() {
    return this.fb.nonNullable.group({
      medicineName: [''],
      dosage: [''],
      frequency: [''],
      durationDays: [''],
      instructions: [''],
    });
  }
}

function todayIso(): string {
  return new Date().toISOString().split('T')[0];
}
