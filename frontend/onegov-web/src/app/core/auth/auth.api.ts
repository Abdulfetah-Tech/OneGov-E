import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from '../api/services.api';
import {
  LoginRequest,
  MfaConfirmed,
  MfaEnrollment,
  RegisterRequest,
  TokenResponse,
  UserProfile,
} from './auth.models';

/** Set on a request that must not trigger a token refresh when it is answered with 401. */
export const SKIP_AUTH_RETRY = new HttpContextToken<boolean>(() => false);

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  register(body: RegisterRequest): Observable<UserProfile> {
    return this.http.post<UserProfile>(`${API_BASE}/auth/register`, body);
  }

  login(body: LoginRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${API_BASE}/auth/login`, body);
  }

  refresh(refreshToken: string): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${API_BASE}/auth/refresh`, { refreshToken });
  }

  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${API_BASE}/auth/logout`, { refreshToken });
  }

  me(skipRetry = false): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${API_BASE}/me`, {
      context: new HttpContext().set(SKIP_AUTH_RETRY, skipRetry),
    });
  }

  beginMfa(password: string): Observable<MfaEnrollment> {
    return this.http.post<MfaEnrollment>(`${API_BASE}/auth/mfa/enroll`, { password });
  }

  confirmMfa(code: string): Observable<MfaConfirmed> {
    return this.http.post<MfaConfirmed>(`${API_BASE}/auth/mfa/confirm`, { code });
  }
}
