import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from './auth.api';
import { TokenResponse, UserProfile } from './auth.models';
import { AuthService } from './auth.service';

const TOKENS: TokenResponse = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-11-22T10:00:00Z',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2026-12-06T10:00:00Z',
  tokenType: 'Bearer',
};

const PROFILE: UserProfile = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'abebe@example.test',
  displayName: 'Abebe',
  language: 'am',
  roles: ['citizen'],
  permissions: ['application:submit'],
  mfaEnabled: false,
  agencyId: null,
};

const failure = (status: number, type?: string): Observable<never> =>
  throwError(() => new HttpErrorResponse({ status, error: type ? { type } : null }));

describe('AuthService', () => {
  let api: {
    register: ReturnType<typeof vi.fn>;
    login: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
    me: ReturnType<typeof vi.fn>;
  };

  function create(): AuthService {
    TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useValue: api as unknown as AuthApi }] });
    return TestBed.inject(AuthService);
  }

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.resetTestingModule();
    api = {
      register: vi.fn(() => of(PROFILE)),
      login: vi.fn(() => of(TOKENS)),
      refresh: vi.fn(() => of({ ...TOKENS, accessToken: 'access-2', refreshToken: 'refresh-2' })),
      logout: vi.fn(() => of(undefined)),
      me: vi.fn(() => of(PROFILE)),
    };
  });

  it('signs in, keeps the access token in memory and the refresh token in session storage', async () => {
    const auth = create();

    const outcome = await auth.login('abebe@example.test', 'a long password');

    expect(outcome).toEqual({ kind: 'signedIn' });
    expect(auth.isSignedIn()).toBe(true);
    expect(auth.user()?.displayName).toBe('Abebe');
    expect(auth.token()).toBe('access-1');
    expect(sessionStorage.getItem('onegov.refresh')).toBe('refresh-1');
  });

  it('asks for a code when the account has two-step verification', async () => {
    api.login.mockReturnValue(failure(401, 'identity.mfa_required'));
    const auth = create();

    const outcome = await auth.login('abebe@example.test', 'a long password');

    expect(outcome).toEqual({ kind: 'mfaRequired' });
    expect(auth.isSignedIn()).toBe(false);
  });

  it('reports the error code for a failed sign-in and stays signed out', async () => {
    api.login.mockReturnValue(failure(401, 'identity.invalid_credentials'));
    const auth = create();

    const outcome = await auth.login('abebe@example.test', 'wrong');

    expect(outcome).toEqual({ kind: 'error', code: 'identity.invalid_credentials' });
    expect(auth.token()).toBeNull();
    expect(sessionStorage.getItem('onegov.refresh')).toBeNull();
  });

  it('restores a session after a reload by refreshing with the stored token', async () => {
    sessionStorage.setItem('onegov.refresh', 'refresh-1');
    const auth = create();

    await auth.init();

    expect(api.refresh).toHaveBeenCalledWith('refresh-1');
    expect(auth.isSignedIn()).toBe(true);
    expect(auth.token()).toBe('access-2');
    expect(sessionStorage.getItem('onegov.refresh')).toBe('refresh-2');
  });

  it('does nothing on start when there is no stored session', async () => {
    const auth = create();

    await auth.init();

    expect(api.refresh).not.toHaveBeenCalled();
    expect(auth.isSignedIn()).toBe(false);
  });

  it('ends the session when the refresh token is rejected', async () => {
    sessionStorage.setItem('onegov.refresh', 'refresh-1');
    api.refresh.mockReturnValue(failure(401, 'identity.invalid_token'));
    const auth = create();

    await auth.init();

    expect(auth.isSignedIn()).toBe(false);
    expect(sessionStorage.getItem('onegov.refresh')).toBeNull();
  });

  it('keeps the stored token when the server cannot be reached, so a later try can succeed', async () => {
    sessionStorage.setItem('onegov.refresh', 'refresh-1');
    api.refresh.mockReturnValue(failure(0));
    const auth = create();

    await auth.init();

    expect(auth.isSignedIn()).toBe(false);
    expect(sessionStorage.getItem('onegov.refresh')).toBe('refresh-1');
  });

  it('shares one refresh request between concurrent callers', async () => {
    sessionStorage.setItem('onegov.refresh', 'refresh-1');
    const auth = create();

    const results = await Promise.all([auth.refresh(), auth.refresh(), auth.refresh()]);

    expect(results).toEqual([true, true, true]);
    expect(api.refresh).toHaveBeenCalledTimes(1);
  });

  it('signs out locally and tells the server to revoke the session', async () => {
    const auth = create();
    await auth.login('abebe@example.test', 'a long password');

    await auth.logout();

    expect(auth.isSignedIn()).toBe(false);
    expect(auth.token()).toBeNull();
    expect(sessionStorage.getItem('onegov.refresh')).toBeNull();
    expect(api.logout).toHaveBeenCalledWith('refresh-1');
  });

  it('answers permission questions from the profile', async () => {
    const auth = create();
    await auth.login('abebe@example.test', 'a long password');

    expect(auth.hasPermission('application:submit')).toBe(true);
    expect(auth.hasPermission('application:decide')).toBe(false);
  });
});
