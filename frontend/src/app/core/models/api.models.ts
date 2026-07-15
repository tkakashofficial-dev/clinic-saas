// Mirrors the backend contracts exactly (backend/Clinic.Application DTOs).
// If the API changes shape, this is the single place to update.

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// ---------- Auth ----------

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  email: string;
  fullName: string;
  /** Primary role (highest privilege) — for display. */
  role: Role;
  /** All roles held — e.g. a doctor-owner has ['Admin', 'Doctor']. */
  roles: Role[];
  tenantId: string;
  tenantUserId: string;
  clinicName: string;
  /** Every clinic this user belongs to — powers the clinic switcher. */
  memberships: Membership[];
  /** SaaS owner (config allowlist) — unlocks the platform console. */
  isPlatformAdmin?: boolean;
  expiresAt: string;
}

export interface Membership {
  tenantId: string;
  clinicName: string;
}

export type Role = 'Admin' | 'Doctor' | 'Receptionist';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  clinicName: string;
  ownerIsDoctor: boolean;
}

// ---------- Patients ----------

export interface PatientDto {
  id: string;
  fullName: string;
  /** Human-friendly per-clinic number, shown as P-000123. */
  patientNumber: number;
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  address: string | null;
  gender: string;
  dateOfBirth: string | null;
  age: number | null;
  medicalConditions: string[];
  registeredAt: string;
}

export interface RegisterPatientRequest {
  firstName: string;
  lastName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  gender: string;
  dateOfBirth?: string | null;
  medicalConditionCodes: string[];
}

export interface PatientHistory {
  patient: PatientDto;
  consultations: PatientConsultation[];
}

export interface PatientConsultation {
  consultationId: string;
  appointmentDate: string;
  recordedAt: string;
  doctorName: string;
  diagnosis: string;
  treatmentNotes: string | null;
  bloodPressure: string | null;
  pulseBpm: number | null;
  temperatureCelsius: number | null;
  weightKg: number | null;
  prescriptionId: string | null;
}

export interface UpdatePatientRequest {
  firstName: string;
  lastName: string;
  phone: string;
  email?: string | null;
  address?: string | null;
  gender: string;
  dateOfBirth?: string | null;
}

// ---------- Appointments ----------

export type AppointmentStatus =
  | 'Scheduled'
  | 'CheckedIn'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

export interface AppointmentDto {
  id: string;
  patientId: string;
  patientName: string;
  patientPhone: string;
  doctorTenantUserId: string;
  doctorName: string;
  appointmentDate: string;
  status: AppointmentStatus;
  notes: string | null;
  createdAt: string;
}

export interface CreateAppointmentRequest {
  patientId: string;
  doctorTenantUserId: string;
  appointmentDate: string;
  notes?: string | null;
}

// ---------- Staff ----------

export interface StaffDto {
  id: string;
  systemUserId: string;
  fullName: string;
  email: string;
  roles: string[];
  isActive: boolean;
  /** Person already had a Klivia account — this clinic was attached to it. */
  existingAccount: boolean;
  createdAt: string;
}

export interface AddStaffRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  roles: string[];
}

// ---------- Consultations & prescriptions ----------

export interface ConsultationDto {
  id: string;
  appointmentId: string;
  diagnosis: string;
  treatmentNotes: string | null;
  bloodPressure: string | null;
  pulseBpm: number | null;
  temperatureCelsius: number | null;
  weightKg: number | null;
  doctorName: string;
  recordedAt: string;
  prescription: PrescriptionDto | null;
}

export interface PrescriptionDto {
  id: string;
  notes: string | null;
  items: PrescriptionItemDto[];
}

export interface PrescriptionItemDto {
  medicineName: string;
  dosage: string | null;
  frequency: string | null;
  durationDays: number | null;
  instructions: string | null;
}

export interface RecordConsultationRequest {
  diagnosis: string;
  treatmentNotes?: string | null;
  bloodPressure?: string | null;
  pulseBpm?: number | null;
  temperatureCelsius?: number | null;
  weightKg?: number | null;
  prescription?: {
    notes?: string | null;
    items: {
      medicineName: string;
      dosage?: string | null;
      frequency?: string | null;
      durationDays?: number | null;
      instructions?: string | null;
    }[];
  } | null;
}

// ---------- Notifications ----------

export interface NotificationDto {
  id: string;
  type: 'Booking' | 'CheckIn' | 'Reminder' | 'Billing';
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}

// ---------- Reports ----------

export interface PracticeOverview {
  totalPatients: number;
  newPatientsLast30Days: number;
  appointmentsToday: number;
  completedLast30Days: number;
  cancelledLast30Days: number;
  appointmentsPerDay: { date: string; count: number }[];
  byStatusLast30Days: { status: string; count: number }[];
  perDoctorLast30Days: { doctorName: string; total: number; completed: number }[];
}

// ---------- Billing ----------

export interface BillingSummary {
  plan: 'Solo' | 'Clinic' | 'Growth';
  isInTrial: boolean;
  /** Trial lapsed without choosing a plan — clinic sits on the Solo floor. */
  trialExpired: boolean;
  trialEndsAt: string | null;
  staffCount: number;
  maxStaff: number;
  doctorCount: number;
  maxDoctors: number;
}

// ---------- Inventory (pharmacy & stores) ----------

export type InventoryCategory = 'Medicine' | 'Consumable' | 'Equipment';

export interface InventoryItemDto {
  id: string;
  name: string;
  category: InventoryCategory;
  unit: string;
  stockQuantity: number;
  reorderLevel: number;
  /** At/below reorder level — time to order more. */
  lowStock: boolean;
  unitPriceRupees: number | null;
  expiryDate: string | null;
  /** Expires within 60 days (or already past). */
  expiringSoon: boolean;
}

export interface SaveInventoryItemRequest {
  name: string;
  category: InventoryCategory;
  unit: string;
  /** Opening stock — only honored on create. */
  stockQuantity: number;
  reorderLevel: number;
  unitPriceRupees?: number | null;
  expiryDate?: string | null;
}

// ---------- Clinic settings ----------

export type IntakeTemplate = 'dental' | 'general';

export interface ClinicSettings {
  name: string;
  phone: string | null;
  address: string | null;
  /** Which seeded intake-form design this clinic prints by default. */
  defaultIntakeTemplate: IntakeTemplate;
}

// ---------- Platform (SaaS owner console) ----------

export interface PlatformTenant {
  tenantId: string;
  name: string;
  plan: 'Solo' | 'Clinic' | 'Growth';
  isInTrial: boolean;
  trialEndsAt: string | null;
  isActive: boolean;
  staffCount: number;
  patientCount: number;
  /** Who to call/WhatsApp for payment — the founding Admin. */
  ownerName: string | null;
  ownerEmail: string | null;
  clinicPhone: string | null;
  clinicAddress: string | null;
  /** Subscription coverage end from recorded payments (null = never paid). */
  paidUntil: string | null;
  lastPaymentAt: string | null;
  lastPaymentAmount: number | null;
  /** Coverage lapsed — time to call them. */
  paymentOverdue: boolean;
  createdAt: string;
}

export type PaymentMethod = 'Upi' | 'BankTransfer' | 'Cash' | 'Other';

export interface RecordPaymentRequest {
  amountRupees: number;
  method: PaymentMethod;
  /** Months of subscription this payment covers. */
  periodMonths: number;
  /** When the money actually arrived (ISO date; default today). */
  paidAt?: string | null;
  note?: string | null;
  /** Optionally apply a plan in the same step. */
  planToApply?: string | null;
}

export interface PlatformPayment {
  paidAt: string;
  amountRupees: number;
  method: PaymentMethod;
  periodMonths: number;
  paidUntil: string;
  note: string | null;
  recordedByEmail: string;
}

export interface PlatformEmailTestResult {
  sent: boolean;
  to: string;
  detail: string;
}

// ---------- Errors (RFC 7807) ----------

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
