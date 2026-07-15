import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateInvoiceRequest,
  InvoiceDto,
  InvoiceStats,
  PagedResult,
} from '../models/api.models';

/** Patient billing — itemised bills, collection, branded PDFs. */
@Injectable({ providedIn: 'root' })
export class InvoicesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/invoices`;

  getAll(options: {
    status?: string;
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<InvoiceDto>> {
    const params: Record<string, string | number> = {};
    if (options.status) params['status'] = options.status;
    if (options.search) params['search'] = options.search;
    params['page'] = options.page ?? 1;
    params['pageSize'] = options.pageSize ?? 20;
    return this.http.get<PagedResult<InvoiceDto>>(this.baseUrl, { params });
  }

  getStats(): Observable<InvoiceStats> {
    return this.http.get<InvoiceStats>(`${this.baseUrl}/stats`);
  }

  create(request: CreateInvoiceRequest): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(this.baseUrl, request);
  }

  markPaid(id: string, paymentMethod: string): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/${id}/mark-paid`, { paymentMethod });
  }

  cancel(id: string): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/${id}/cancel`, {});
  }

  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/pdf`, { responseType: 'blob' });
  }
}
