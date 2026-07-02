import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { NotificationDto, PagedResult } from '../models/api.models';

const POLL_INTERVAL_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/notification`;

  /** Live unread badge — polled every minute while the shell is open. */
  readonly unreadCount = signal(0);

  private pollHandle: ReturnType<typeof setInterval> | null = null;

  startPolling(): void {
    if (this.pollHandle !== null) return;
    this.refreshUnreadCount();
    this.pollHandle = setInterval(() => this.refreshUnreadCount(), POLL_INTERVAL_MS);
  }

  stopPolling(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
  }

  refreshUnreadCount(): void {
    this.http.get<number>(`${this.baseUrl}/unread-count`).subscribe({
      next: (count) => this.unreadCount.set(count),
      error: () => {}, // badge is non-critical; never bother the user
    });
  }

  getMine(page = 1, pageSize = 20): Observable<PagedResult<NotificationDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<NotificationDto>>(this.baseUrl, { params });
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, {});
  }
}
