import { Injectable, signal } from '@angular/core';

/**
 * Chrome's install-prompt event — not yet in the TypeScript DOM lib.
 * Fired once per page load when the browser decides the app is installable.
 */
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

/**
 * Owns the PWA install flow. Chrome only shows its own install banner
 * "sometimes" — so we intercept the event and replay it from our own
 * "Install app" button, whenever the user wants it. On iOS (no prompt
 * API at all) the UI falls back to Add-to-Home-Screen instructions.
 */
@Injectable({ providedIn: 'root' })
export class PwaInstallService {
  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  /** The browser handed us a prompt we can replay from our own button. */
  readonly canInstall = signal(false);

  /** Already running from the installed icon (standalone window). */
  readonly isInstalled = signal(
    window.matchMedia('(display-mode: standalone)').matches ||
      ('standalone' in navigator && (navigator as { standalone?: boolean }).standalone === true),
  );

  /** iOS never fires beforeinstallprompt — needs Share → Add to Home Screen. */
  readonly isIos =
    /iphone|ipad|ipod/i.test(navigator.userAgent) ||
    // iPadOS 13+ reports itself as a Mac, but Macs have no touch points
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

  constructor() {
    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault(); // silence Chrome's banner — OUR button decides when
      this.deferredPrompt = event as BeforeInstallPromptEvent;
      this.canInstall.set(true);
    });

    window.addEventListener('appinstalled', () => {
      this.deferredPrompt = null;
      this.canInstall.set(false);
      this.isInstalled.set(true);
    });
  }

  /** Replays the browser's install prompt. Resolves true if accepted. */
  async promptInstall(): Promise<boolean> {
    const prompt = this.deferredPrompt;
    if (!prompt) return false;

    await prompt.prompt();
    const choice = await prompt.userChoice;

    // The event is single-use: Chrome fires a fresh one if still installable
    this.deferredPrompt = null;
    this.canInstall.set(false);
    return choice.outcome === 'accepted';
  }
}
