import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AppointmentDto,
  ConsultationDto,
  CreateAppointmentRequest,
  PagedResult,
  RecordConsultationRequest,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AppointmentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/appointment`;

  getAll(options: {
    date?: string;
    status?: string;
    doctorTenantUserId?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<AppointmentDto>> {
    let params = new HttpParams()
      .set('page', options.page ?? 1)
      .set('pageSize', options.pageSize ?? 10);
    if (options.date) params = params.set('date', options.date);
    if (options.status) params = params.set('status', options.status);
    if (options.doctorTenantUserId) {
      params = params.set('doctorTenantUserId', options.doctorTenantUserId);
    }
    return this.http.get<PagedResult<AppointmentDto>>(this.baseUrl, { params });
  }

  create(request: CreateAppointmentRequest): Observable<AppointmentDto> {
    return this.http.post<AppointmentDto>(this.baseUrl, request);
  }

  updateStatus(id: string, status: string): Observable<AppointmentDto> {
    return this.http.patch<AppointmentDto>(`${this.baseUrl}/${id}/status`, { status });
  }

  recordConsultation(
    appointmentId: string,
    request: RecordConsultationRequest,
  ): Observable<ConsultationDto> {
    return this.http.post<ConsultationDto>(
      `${this.baseUrl}/${appointmentId}/consultation`, request);
  }

  getConsultation(appointmentId: string): Observable<ConsultationDto> {
    return this.http.get<ConsultationDto>(`${this.baseUrl}/${appointmentId}/consultation`);
  }

  downloadPrescriptionPdf(prescriptionId: string): Observable<Blob> {
    return this.http.get(
      `${environment.apiUrl}/prescription/${prescriptionId}/pdf`,
      { responseType: 'blob' });
  }
}
