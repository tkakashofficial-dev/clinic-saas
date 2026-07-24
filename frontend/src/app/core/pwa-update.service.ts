import { Injectable, inject, signal } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs';

/**
 * Watches for a newly deployed version and surfaces a one-tap refresh.
 * Without this, an installed PWA can run a stale build for days — reception
 * desktops that never close the tab would never pick up a fix.
 */
@Injectable({ providedIn: 'root' })
export class PwaUpdateService {
  private readonly updates = inject(SwUpdate);

  /** True once a new version is downloaded and ready to activate. */
  readonly updateReady = signal(false);

  constructor() {
    if (!this.updates.isEnabled) return;

    this.updates.versionUpdates
      .pipe(filter((e) => e.type === 'VERSION_READY'))
      .subscribe(() => this.updateReady.set(true));

    // A broken/unrecoverable SW state: force a clean reload rather than
    // leaving the user on a half-cached app
    this.updates.unrecoverable.subscribe(() => document.location.reload());

    // Always-open reception screens: check for a new build every 6 hours
    setInterval(() => void this.updates.checkForUpdate(), 6 * 60 * 60 * 1000);
  }

  reload(): void {
    document.location.reload();
  }
}
