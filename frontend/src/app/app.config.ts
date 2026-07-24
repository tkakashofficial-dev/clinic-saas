import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEnIn from '@angular/common/locales/en-IN';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  PreloadAllModules,
  provideRouter,
  withInMemoryScrolling,
  withPreloading,
  withViewTransitions,
} from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';

import { routes } from './app.routes';
import { loadingInterceptor } from './core/api/loading.service';
import { authInterceptor } from './core/auth/auth.interceptor';
import { PwaInstallService } from './core/pwa-install.service';
import { PwaUpdateService } from './core/pwa-update.service';

// Indian number grouping (₹1,00,000 not ₹100,000) for every date/number pipe
registerLocaleData(localeEnIn);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: LOCALE_ID, useValue: 'en-IN' },
    provideRouter(
      routes,
      // Lazy chunks download in the background after first paint —
      // every navigation after that is instant, zero spinner
      withPreloading(PreloadAllModules),
      // Native browser View Transitions: pages morph instead of blink
      withViewTransitions(),
      // Restore scroll on back/forward; jump to #fragment anchors (landing nav)
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
    ),
    provideHttpClient(withInterceptors([authInterceptor, loadingInterceptor])),
    // PWA: installable on phones/desktops, instant loads from cache.
    // Registers after the app is stable so it never slows first paint.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    // Chrome fires beforeinstallprompt very early — the listener must
    // exist at bootstrap or the "Install app" button never activates.
    // PwaUpdateService starts watching for new deploys at the same time.
    provideAppInitializer(() => {
      inject(PwaInstallService);
      inject(PwaUpdateService);
    }),
  ],
};
