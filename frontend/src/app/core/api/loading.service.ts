import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

/**
 * Global "something is happening" state. A cold free-tier server can take
 * 30–60s — without visible motion users think the app froze and start
 * rage-clicking. The shell renders a slim animated bar whenever any
 * request is in flight (except background polling, which would make it
 * flicker forever).
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly inFlight = signal(0);

  readonly isLoading = computed(() => this.inFlight() > 0);

  begin(): void {
    this.inFlight.update((count) => count + 1);
  }

  end(): void {
    this.inFlight.update((count) => Math.max(0, count - 1));
  }
}

/** Requests that repeat silently in the background — never show the bar. */
const SILENT_URLS = ['/notification'];

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (SILENT_URLS.some((fragment) => req.url.includes(fragment))) {
    return next(req);
  }

  const loading = inject(LoadingService);
  loading.begin();
  return next(req).pipe(finalize(() => loading.end()));
};
