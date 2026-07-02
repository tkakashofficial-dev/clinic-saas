import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BillingSummary } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/billing`;

  /**
   * Single shared copy of the summary: the shell's trial chip and the billing
   * page read the SAME signal, so a plan change updates both instantly
   * (previously each held its own copy and the chip went stale).
   */
  readonly summary = signal<BillingSummary | null>(null);

  getSummary(): Observable<BillingSummary> {
    return this.http
      .get<BillingSummary>(`${this.baseUrl}/summary`)
      .pipe(tap((summary) => this.summary.set(summary)));
  }

  changePlan(plan: string): Observable<BillingSummary> {
    return this.http
      .post<BillingSummary>(`${this.baseUrl}/change-plan`, { plan })
      .pipe(tap((summary) => this.summary.set(summary)));
  }
}
