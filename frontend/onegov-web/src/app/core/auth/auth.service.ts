import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from './auth.api';
import { NationalIdStatus, RegisterRequest, TokenResponse, UserProfile } from './auth.models';
import { problemCode } from './auth.utils';

const REFRESH_KEY = 'onegov.refresh';
const NATIONAL_ID_STATE_KEY = 'onegov.nid.state';

export type LoginOutcome =
  | { kind: 'signedIn' }
  | { kind: 'mfaRequired' }
  | { kind: 'error'; code: string };

export type NationalIdBegin = { ok: true; url: string } | { ok: false; code: string };

export type RegisterOutcome = { ok: true } | { ok: false; code: string };

/**
 * Holds who is signed in. The short-lived access token lives only in memory. The refresh token is kept in
 * sessionStorage so a page reload keeps the session but closing the tab ends it. Anything in browser storage can be
 * read by a script injected into the page, so this app loads no third-party scripts, and the choice is to be revisited
 * (HttpOnly cookie) before production. See docs/adr/0005-identity.md.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly accessToken = signal<string | null>(null);
  private refreshInFlight: Promise<boolean> | null = null;

  readonly user = signal<UserProfile | null>(null);
  readonly isSignedIn = computed(() => this.user() !== null);

  token(): string | null {
    return this.accessToken();
  }

  hasPermission(permission: string): boolean {
    return this.user()?.permissions.includes(permission) ?? false;
  }

  /** Restores a session after a reload. Never throws, so the app always starts. */
  async init(): Promise<void> {
    if (this.storedRefresh()) {
      await this.refresh();
    }
  }

  /** Exchanges the refresh token for new tokens. Concurrent callers share one request, because each token works once. */
  refresh(): Promise<boolean> {
    this.refreshInFlight ??= this.doRefresh().finally(() => {
      this.refreshInFlight = null;
    });
    return this.refreshInFlight;
  }

  async register(request: RegisterRequest): Promise<RegisterOutcome> {
    try {
      await firstValueFrom(this.api.register(request));
      return { ok: true };
    } catch (error) {
      return { ok: false, code: problemCode(error) };
    }
  }

  async login(email: string, password: string, otpCode?: string): Promise<LoginOutcome> {
    try {
      const tokens = await firstValueFrom(this.api.login({ email, password, otpCode: otpCode || null }));
      this.accept(tokens);
      await this.loadProfile();
      return { kind: 'signedIn' };
    } catch (error) {
      const code = problemCode(error);
      if (code === 'identity.mfa_required') {
        return { kind: 'mfaRequired' };
      }
      this.clear();
      return { kind: 'error', code };
    }
  }

  async nationalIdStatus(): Promise<NationalIdStatus> {
    try {
      return await firstValueFrom(this.api.nationalIdStatus());
    } catch {
      return { available: false, isMock: false };
    }
  }

  /** Asks the API where to send the browser and remembers the state, which must come back unchanged. */
  async beginNationalId(): Promise<NationalIdBegin> {
    try {
      const start = await firstValueFrom(this.api.startNationalId());
      try {
        sessionStorage.setItem(NATIONAL_ID_STATE_KEY, start.state);
      } catch {
        return { ok: false, code: 'unknown' };
      }
      return { ok: true, url: start.authorizationUrl };
    } catch (error) {
      return { ok: false, code: problemCode(error) };
    }
  }

  /** Finishes national ID sign-in after the provider sends the browser back. The returned state must be the one we sent. */
  async completeNationalId(code: string, state: string): Promise<LoginOutcome> {
    const expected = this.takeNationalIdState();
    if (!expected || expected !== state) {
      return { kind: 'error', code: 'identity.invalid_state' };
    }
    try {
      this.accept(await firstValueFrom(this.api.completeNationalId(code, state)));
      await this.loadProfile();
      return { kind: 'signedIn' };
    } catch (error) {
      this.clear();
      return { kind: 'error', code: problemCode(error) };
    }
  }

  async logout(): Promise<void> {
    const stored = this.storedRefresh();
    this.clear();
    if (stored) {
      try {
        await firstValueFrom(this.api.logout(stored));
      } catch {
        // Best effort: the session is already gone on this device.
      }
    }
  }

  async reloadProfile(): Promise<void> {
    await this.loadProfile();
  }

  private async doRefresh(): Promise<boolean> {
    const stored = this.storedRefresh();
    if (!stored) {
      this.clear();
      return false;
    }
    try {
      this.accept(await firstValueFrom(this.api.refresh(stored)));
      if (!this.user()) {
        await this.loadProfile();
      }
      return true;
    } catch (error) {
      // Only an explicit rejection ends the session. A network failure keeps the token so the next try can succeed.
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.clear();
      }
      return false;
    }
  }

  private async loadProfile(): Promise<void> {
    this.user.set(await firstValueFrom(this.api.me(true)));
  }

  private accept(tokens: TokenResponse): void {
    this.accessToken.set(tokens.accessToken);
    try {
      sessionStorage.setItem(REFRESH_KEY, tokens.refreshToken);
    } catch {
      // Without storage the session lasts until the page is reloaded.
    }
  }

  private clear(): void {
    this.accessToken.set(null);
    this.user.set(null);
    try {
      sessionStorage.removeItem(REFRESH_KEY);
    } catch {
      // Ignore.
    }
  }

  private takeNationalIdState(): string | null {
    try {
      const value = sessionStorage.getItem(NATIONAL_ID_STATE_KEY);
      sessionStorage.removeItem(NATIONAL_ID_STATE_KEY);
      return value;
    } catch {
      return null;
    }
  }

  private storedRefresh(): string | null {
    try {
      return sessionStorage.getItem(REFRESH_KEY);
    } catch {
      return null;
    }
  }
}
