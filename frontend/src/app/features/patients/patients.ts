import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { parseApiError } from '../../core/api/api-error';
import { AppointmentsService } from '../../core/api/appointments.service';
import { FormsService } from '../../core/api/forms.service';
import { PatientsService } from '../../core/api/patients.service';
import { SettingsService } from '../../core/api/settings.service';
import { AuthService } from '../../core/auth/auth.service';
import {
  IntakeFormResponse,
  IntakeFormSection,
  IntakeTemplate,
  PagedResult,
  PatientDto,
  PatientHistory,
} from '../../core/models/api.models';
import { DateField } from '../../shared/ui/date-field';
import { Segmented } from '../../shared/ui/segmented';

@Component({
  selector: 'app-patients',
  imports: [DatePipe, ReactiveFormsModule, FormsModule, Segmented, DateField],
  templateUrl: './patients.html',
  styleUrl: './patients.scss',
})
export class Patients {
  readonly auth = inject(AuthService);
  private readonly api = inject(PatientsService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly result = signal<PagedResult<PatientDto> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);

  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});
  /** Non-null while editing an existing patient (drawer doubles as edit form). */
  readonly editing = signal<PatientDto | null>(null);

  // ---- patient history drawer ----
  private readonly appointmentsApi = inject(AppointmentsService);
  private readonly settingsApi = inject(SettingsService);
  readonly historyOpen = signal(false);
  readonly historyLoading = signal(false);

  /** Print buttons ordered so the clinic's chosen template leads (★). */
  readonly intakeTemplates = computed(() => {
    const defaultTemplate = this.settingsApi.settings()?.defaultIntakeTemplate ?? 'dental';
    const all = [
      { key: 'dental' as const, emoji: '🦷', label: 'Dental intake form' },
      { key: 'general' as const, emoji: '🩺', label: 'General intake form' },
    ];
    return all
      .map((t) => ({ ...t, isDefault: t.key === defaultTemplate }))
      .sort((a, b) => Number(b.isDefault) - Number(a.isDefault));
  });
  readonly history = signal<PatientHistory | null>(null);
  /** Which template is currently downloading ('dental' | 'general'), or null. */
  readonly downloadingForm = signal<string | null>(null);

  openHistory(patient: PatientDto): void {
    this.historyOpen.set(true);
    this.historyLoading.set(true);
    this.history.set(null);
    this.latestFill.set(null);
    this.api.getHistory(patient.id).subscribe({
      next: (history) => {
        this.history.set(history);
        this.historyLoading.set(false);
      },
      error: () => this.historyLoading.set(false),
    });
    // In parallel: has this patient's form been filled digitally?
    this.formsApi.latestResponse(patient.id).subscribe({
      next: (response) => this.latestFill.set(response),
      error: () => {},
    });
  }

  // ---- fill the intake form digitally (staff asks the patient) ----
  private readonly formsApi = inject(FormsService);
  readonly latestFill = signal<IntakeFormResponse | null>(null);
  readonly fillOpen = signal(false);
  readonly fillSaving = signal(false);
  readonly fillError = signal('');
  readonly fillSections = signal<IntakeFormSection[]>([]);
  fillTemplate: IntakeTemplate = 'dental';

  readonly diseaseChecklist = [
    'Diabetic', 'Blood Pressure', 'Drug Allergies', 'Latex Allergy',
    'Cardiac', 'Anticoagulant Use', 'Pregnancy', 'Epilepsy',
    'Hepatic', 'Renal', 'Pulmonary', 'STD',
  ];
  checkedDiseases = new Set<string>();
  fillChiefComplaint = '';
  fillMedicalHistory = '';
  fillSecondaryHistory = '';
  fillMedications = '';
  /** sectionId → answer state for the clinic's custom sections. */
  customText: Record<string, string> = {};
  customChecked: Record<string, Set<string>> = {};
  customLines: Record<string, Record<string, string>> = {};

  openFill(): void {
    const previous = this.latestFill();
    this.fillTemplate = previous?.template
      ?? this.settingsApi.settings()?.defaultIntakeTemplate ?? 'dental';

    // Pre-load previous answers so staff UPDATE instead of retyping
    const answers = previous?.answers;
    this.checkedDiseases = new Set(answers?.diseaseChecklist ?? []);
    this.fillChiefComplaint = answers?.chiefComplaint ?? '';
    this.fillMedicalHistory = answers?.medicalHistory ?? '';
    this.fillSecondaryHistory = answers?.secondaryHistory ?? '';
    this.fillMedications = answers?.medications ?? '';
    this.customText = {};
    this.customChecked = {};
    this.customLines = {};
    for (const custom of answers?.custom ?? []) {
      this.customText[custom.sectionId] = custom.text ?? '';
      this.customChecked[custom.sectionId] = new Set(custom.checked);
      this.customLines[custom.sectionId] = { ...custom.lines };
    }

    this.fillError.set('');
    this.fillOpen.set(true);
    this.formsApi.getSections().subscribe({
      next: (sections) => this.fillSections.set(
        sections.filter((s) => s.template === 'both' || s.template === this.fillTemplate)),
      error: () => {},
    });
  }

  toggleDisease(disease: string): void {
    if (this.checkedDiseases.has(disease)) this.checkedDiseases.delete(disease);
    else this.checkedDiseases.add(disease);
  }

  toggleCustomCheck(sectionId: string, item: string): void {
    const set = (this.customChecked[sectionId] ??= new Set<string>());
    if (set.has(item)) set.delete(item);
    else set.add(item);
  }

  lineValue(sectionId: string, item: string): string {
    return this.customLines[sectionId]?.[item] ?? '';
  }

  setLineValue(sectionId: string, item: string, value: string): void {
    (this.customLines[sectionId] ??= {})[item] = value;
  }

  saveFill(): void {
    const patient = this.history()?.patient;
    if (!patient) return;

    this.fillSaving.set(true);
    this.fillError.set('');

    this.formsApi.saveResponse(patient.id, this.fillTemplate, {
      diseaseChecklist: [...this.checkedDiseases],
      chiefComplaint: this.fillChiefComplaint.trim() || null,
      medicalHistory: this.fillMedicalHistory.trim() || null,
      secondaryHistory: this.fillSecondaryHistory.trim() || null,
      medications: this.fillMedications.trim() || null,
      custom: this.fillSections().map((section) => ({
        sectionId: section.id,
        text: this.customText[section.id]?.trim() || null,
        checked: [...(this.customChecked[section.id] ?? [])],
        lines: this.customLines[section.id] ?? {},
      })),
    }).subscribe({
      next: (response) => {
        this.latestFill.set(response);
        this.fillSaving.set(false);
        this.fillOpen.set(false);
      },
      error: (err) => {
        this.fillSaving.set(false);
        this.fillError.set(parseApiError(err).message);
      },
    });
  }

  initialsOf(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .map((part) => part[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  editFromHistory(): void {
    const patient = this.history()?.patient;
    if (!patient) return;
    this.historyOpen.set(false);
    this.openEdit(patient);
  }

  printIntakeForm(template: 'dental' | 'general'): void {
    const patient = this.history()?.patient;
    if (!patient) return;
    this.downloadingForm.set(template);
    this.api.downloadIntakeForm(patient.id, template).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `intake-${template}-P${String(patient.patientNumber).padStart(6, '0')}.pdf`;
        link.click();
        URL.revokeObjectURL(url);
        this.downloadingForm.set(null);
      },
      error: () => this.downloadingForm.set(null),
    });
  }

  downloadPrescription(prescriptionId: string): void {
    this.appointmentsApi.downloadPrescriptionPdf(prescriptionId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'prescription.pdf';
        link.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  patientCode(patient: PatientDto): string {
    return `P-${String(patient.patientNumber).padStart(6, '0')}`;
  }

  private readonly searchInput$ = new Subject<string>();

  readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phone: ['', Validators.required],
    email: [''],
    address: [''],
    gender: ['Male', Validators.required],
    dateOfBirth: [''],
  });

  constructor() {
    // Debounce keystrokes — search fires 300ms after typing stops,
    // not on every letter
    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => {
        this.search.set(term);
        this.page.set(1);
        this.load();
      });

    // Clinic settings drive which intake template leads in the drawer
    if (!this.settingsApi.settings()) {
      this.settingsApi.get().subscribe({ error: () => {} });
    }

    this.load();
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  load(): void {
    this.loading.set(true);
    this.api.getAll(this.search(), this.page()).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  openDrawer(): void {
    this.editing.set(null);
    this.form.reset({ gender: 'Male' });
    this.formError.set('');
    this.fieldErrors.set({});
    this.drawerOpen.set(true);
  }

  openEdit(patient: PatientDto): void {
    this.editing.set(patient);
    this.form.reset({
      firstName: patient.firstName,
      lastName: patient.lastName,
      phone: patient.phone,
      email: patient.email ?? '',
      address: patient.address ?? '',
      gender: patient.gender,
      dateOfBirth: patient.dateOfBirth ?? '',
    });
    this.formError.set('');
    this.fieldErrors.set({});
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.formError.set('');
    this.fieldErrors.set({});

    const value = this.form.getRawValue();
    const payload = {
      firstName: value.firstName,
      lastName: value.lastName,
      phone: value.phone,
      email: value.email || null,
      address: value.address || null,
      gender: value.gender,
      dateOfBirth: value.dateOfBirth || null,
    };

    const editing = this.editing();
    const request = editing
      ? this.api.update(editing.id, payload)
      : this.api.register({ ...payload, medicalConditionCodes: [] });

    request
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.drawerOpen.set(false);
          this.load();
        },
        error: (err) => {
          const parsed = parseApiError(err);
          this.formError.set(Object.keys(parsed.fieldErrors).length ? '' : parsed.message);
          this.fieldErrors.set(parsed.fieldErrors);
          this.saving.set(false);
        },
      });
  }

  errorFor(control: string): string {
    const c = this.form.get(control);
    if (c?.invalid && c.touched && c.errors?.['required']) return 'This field is required.';
    return this.fieldErrors()[control] ?? '';
  }
}
