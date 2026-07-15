import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IntakeFormSection, SaveIntakeFormSectionRequest } from '../models/api.models';

/** The clinic's form builder: custom intake sections + sample previews. */
@Injectable({ providedIn: 'root' })
export class FormsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/forms`;

  getSections(): Observable<IntakeFormSection[]> {
    return this.http.get<IntakeFormSection[]>(`${this.baseUrl}/sections`);
  }

  createSection(request: SaveIntakeFormSectionRequest): Observable<IntakeFormSection> {
    return this.http.post<IntakeFormSection>(`${this.baseUrl}/sections`, request);
  }

  deleteSection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/sections/${id}`);
  }

  /** direction: -1 = up, 1 = down. Returns the fresh ordering. */
  moveSection(id: string, direction: 1 | -1): Observable<IntakeFormSection[]> {
    return this.http.post<IntakeFormSection[]>(
      `${this.baseUrl}/sections/${id}/move`, {}, { params: { direction } });
  }

  /** The form rendered with SAMPLE data — exactly what will print. */
  preview(template: 'dental' | 'general'): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/preview`, {
      params: { template },
      responseType: 'blob',
    });
  }
}
