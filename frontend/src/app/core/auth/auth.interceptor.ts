import {
  HttpErrorResponse,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * 1. Attaches the JWT to every API request.
 * 2. On 401: silently exchanges the refresh token for a new pair and retries
 *    the request ONCE. If refresh fails too, the session is over — logout.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);

  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthCall = request.url.includes('/auth/');
      if (error.status !== 401 || isAuthCall || !auth.isLoggedIn()) {
        return throwError(() => error);
      }

      // Access token expired — try one silent refresh, then retry the call
      return auth.refresh().pipe(
        switchMap(() => next(withToken(request, auth.accessToken()))),
        catchError((refreshError) => {
          auth.logout();
          return throwError(() => refreshError);
        }),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;
}
