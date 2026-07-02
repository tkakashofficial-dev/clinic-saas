import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PracticeOverview } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);

  getOverview(): Observable<PracticeOverview> {
    return this.http.get<PracticeOverview>(`${environment.apiUrl}/reports/overview`);
  }
}
