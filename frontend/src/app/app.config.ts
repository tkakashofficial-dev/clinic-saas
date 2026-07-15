import { ApplicationConfig, isDevMode, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { PreloadAllModules, provideRouter, withPreloading, withViewTransitions } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';

import { routes } from './app.routes';
import { loadingInterceptor } from './core/api/loading.service';
import { authInterceptor } from './core/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Lazy chunks download in the background after first paint —
      // every navigation after that is instant, zero spinner
      withPreloading(PreloadAllModules),
      // Native browser View Transitions: pages morph instead of blink
      withViewTransitions(),
    ),
    provideHttpClient(withInterceptors([authInterceptor, loadingInterceptor])),
    // PWA: installable on phones/desktops, instant loads from cache.
    // Registers after the app is stable so it never slows first paint.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
