import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AddStaffRequest, PagedResult, StaffDto } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class StaffService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/staff`;

  getAll(page = 1, pageSize = 50): Observable<PagedResult<StaffDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<StaffDto>>(this.baseUrl, { params });
  }

  add(request: AddStaffRequest): Observable<StaffDto> {
    return this.http.post<StaffDto>(this.baseUrl, request);
  }
}
