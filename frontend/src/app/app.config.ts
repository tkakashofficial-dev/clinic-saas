import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { PreloadAllModules, provideRouter, withPreloading, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
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
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
