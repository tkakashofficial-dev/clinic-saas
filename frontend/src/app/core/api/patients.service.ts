import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  PatientDto,
  PatientHistory,
  RegisterPatientRequest,
  UpdatePatientRequest,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PatientsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/patient`;

  getAll(search: string, page: number, pageSize = 10): Observable<PagedResult<PatientDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<PatientDto>>(this.baseUrl, { params });
  }

  getById(id: string): Observable<PatientDto> {
    return this.http.get<PatientDto>(`${this.baseUrl}/${id}`);
  }

  register(request: RegisterPatientRequest): Observable<PatientDto> {
    return this.http.post<PatientDto>(this.baseUrl, request);
  }

  update(id: string, request: UpdatePatientRequest): Observable<PatientDto> {
    return this.http.put<PatientDto>(`${this.baseUrl}/${id}`, request);
  }

  getHistory(id: string): Observable<PatientHistory> {
    return this.http.get<PatientHistory>(`${this.baseUrl}/${id}/history`);
  }

  /** template: 'dental' | 'general' — the two seeded intake-form designs. */
  downloadIntakeForm(id: string, template: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/intake-form`, {
      params: { template },
      responseType: 'blob',
    });
  }
}
