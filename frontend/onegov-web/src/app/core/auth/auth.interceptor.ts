import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { API_BASE } from '../api/services.api';
import { SKIP_AUTH_RETRY } from './auth.api';
import { AuthService } from './auth.service';

// Endpoints that work without signing in. The access token is not sent to them.
const PUBLIC_PREFIXES = ['/services', '/auth/register', '/auth/login', '/auth/refresh', '/auth/logout'];

/** Adds the access token to API calls. On a 401 it refreshes once and retries, then gives up. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const isApi = req.url.startsWith(API_BASE);
  const isPublic = PUBLIC_PREFIXES.some((prefix) => req.url.startsWith(`${API_BASE}${prefix}`));
  if (!isApi || isPublic) {
    return next(req);
  }

  const auth = inject(AuthService);
  const send = (token: string | null) =>
    next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);

  return send(auth.token()).pipe(
    catchError((error: unknown) => {
      const canRetry =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        auth.token() !== null &&
        !req.context.get(SKIP_AUTH_RETRY);
      if (!canRetry) {
        return throwError(() => error);
      }
      return from(auth.refresh()).pipe(
        switchMap((refreshed) => (refreshed ? send(auth.token()) : throwError(() => error))),
      );
    }),
  );
};
