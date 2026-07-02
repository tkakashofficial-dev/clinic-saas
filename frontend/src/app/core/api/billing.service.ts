import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BillingSummary } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/billing`;

  getSummary(): Observable<BillingSummary> {
    return this.http.get<BillingSummary>(`${this.baseUrl}/summary`);
  }

  changePlan(plan: string): Observable<BillingSummary> {
    return this.http.post<BillingSummary>(`${this.baseUrl}/change-plan`, { plan });
  }
}
