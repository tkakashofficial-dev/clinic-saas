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
  role: Role;
  tenantId: string;
  tenantUserId: string;
  expiresAt: string;
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
}

// ---------- Patients ----------

export interface PatientDto {
  id: string;
  fullName: string;
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
  role: string;
  isActive: boolean;
  createdAt: string;
}

export interface AddStaffRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  role: string;
}

// ---------- Consultations & prescriptions ----------

export interface ConsultationDto {
  id: string;
  appointmentId: string;
  diagnosis: string;
  treatmentNotes: string | null;
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

// ---------- Errors (RFC 7807) ----------

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
