import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from '../models/api.models';

/** Blocks the app shell for anonymous visitors — they see the landing page. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.createUrlTree(['/welcome']);
};

/** Sends logged-in users away from login/register. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? router.createUrlTree(['/']) : true;
};

/** Platform console is for the SaaS owner only (server re-verifies too). */
export const platformGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isPlatformAdmin() ? true : router.createUrlTree(['/']);
};

/** Role-based route protection: route data `{ roles: ['Admin'] }`. */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const allowed = (route.data['roles'] ?? []) as Role[];
  return allowed.length === 0 || auth.hasRole(...allowed)
    ? true
    : router.createUrlTree(['/']);
};
