import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PlatformEmailTestResult, PlatformPayment, PlatformTenant, RecordPaymentRequest } from '../models/api.models';

/**
 * SaaS-owner console API. Every endpoint re-checks the server-side email
 * allowlist — the frontend gate is UX, not security.
 */
@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/platform`;

  getTenants(): Observable<PlatformTenant[]> {
    return this.http.get<PlatformTenant[]>(`${this.baseUrl}/tenants`);
  }

  /** Applied after payment is confirmed (manual UPI today, Razorpay later). */
  changePlan(tenantId: string, plan: string): Observable<PlatformTenant> {
    return this.http.post<PlatformTenant>(
      `${this.baseUrl}/tenants/${tenantId}/change-plan`, { plan });
  }

  setActive(tenantId: string, isActive: boolean): Observable<PlatformTenant> {
    return this.http.post<PlatformTenant>(
      `${this.baseUrl}/tenants/${tenantId}/set-active`, { isActive });
  }

  /** Records a collected payment (UPI/bank/cash) and extends coverage. */
  recordPayment(tenantId: string, request: RecordPaymentRequest): Observable<PlatformTenant> {
    return this.http.post<PlatformTenant>(
      `${this.baseUrl}/tenants/${tenantId}/record-payment`, request);
  }

  /** A clinic's full payment history, newest first. */
  getPayments(tenantId: string): Observable<PlatformPayment[]> {
    return this.http.get<PlatformPayment[]>(`${this.baseUrl}/tenants/${tenantId}/payments`);
  }

  /** Sends a REAL email to the platform admin — proves production SMTP works. */
  testEmail(): Observable<PlatformEmailTestResult> {
    return this.http.post<PlatformEmailTestResult>(`${this.baseUrl}/test-email`, {});
  }
}
