import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { ClinicSettings } from '../models/api.models';

/**
 * Clinic settings — the letterhead (name/phone/address printed on every PDF)
 * and template preferences. Shared signal so the patients screen knows the
 * default intake template without refetching.
 */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings`;

  readonly settings = signal<ClinicSettings | null>(null);

  constructor() {
    // Wipe cached clinic settings on logout (shared-PC hygiene)
    inject(AuthService).onLogout(() => this.settings.set(null));
  }

  get(): Observable<ClinicSettings> {
    return this.http
      .get<ClinicSettings>(this.baseUrl)
      .pipe(tap((settings) => this.settings.set(settings)));
  }

  update(request: ClinicSettings): Observable<ClinicSettings> {
    return this.http
      .put<ClinicSettings>(this.baseUrl, request)
      .pipe(tap((settings) => this.settings.set(settings)));
  }
}
