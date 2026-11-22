import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { errorKey, problemCode, safeReturnUrl } from './auth.utils';

describe('safeReturnUrl', () => {
  it('keeps same-site paths', () => {
    expect(safeReturnUrl('/services/TAX-FILING')).toBe('/services/TAX-FILING');
    expect(safeReturnUrl('/account?tab=mfa')).toBe('/account?tab=mfa');
  });

  it('falls back for anything that could leave the site or is empty', () => {
    for (const value of ['https://evil.example', '//evil.example', '/\\evil.example', 'account', '', null, undefined]) {
      expect(safeReturnUrl(value)).toBe('/account');
    }
  });
});

describe('problemCode', () => {
  it('reads the identity error type from the API problem response', () => {
    const error = new HttpErrorResponse({ status: 401, error: { type: 'identity.invalid_credentials' } });
    expect(problemCode(error)).toBe('identity.invalid_credentials');
  });

  it('recognises network failures and rate limiting', () => {
    expect(problemCode(new HttpErrorResponse({ status: 0 }))).toBe('network');
    expect(problemCode(new HttpErrorResponse({ status: 429 }))).toBe('rate_limited');
  });

  it('is unknown for anything else', () => {
    expect(problemCode(new HttpErrorResponse({ status: 500, error: 'boom' }))).toBe('unknown');
    expect(problemCode(new HttpErrorResponse({ status: 400, error: { type: 'something-else' } }))).toBe('unknown');
    expect(problemCode(new Error('x'))).toBe('unknown');
  });
});

describe('errorKey', () => {
  it('maps known codes to translation keys', () => {
    expect(errorKey('identity.invalid_credentials')).toBe('error.invalid_credentials');
    expect(errorKey('rate_limited')).toBe('error.rate_limited');
    expect(errorKey('network')).toBe('error.network');
  });

  it('uses the generic message for unknown codes', () => {
    expect(errorKey('identity.something_new')).toBe('error.unknown');
    expect(errorKey('unknown')).toBe('error.unknown');
  });
});
