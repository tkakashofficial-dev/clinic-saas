import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PublicBookingRequest,
  PublicBookingResult,
  PublicClinic,
} from '../models/api.models';

/** Anonymous booking API behind /book/:slug — no auth token involved. */
@Injectable({ providedIn: 'root' })
export class PublicBookingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/public`;

  getClinic(slug: string): Observable<PublicClinic> {
    return this.http.get<PublicClinic>(`${this.baseUrl}/${slug}`);
  }

  book(slug: string, request: PublicBookingRequest): Observable<PublicBookingResult> {
    return this.http.post<PublicBookingResult>(`${this.baseUrl}/${slug}/book`, request);
  }
}
