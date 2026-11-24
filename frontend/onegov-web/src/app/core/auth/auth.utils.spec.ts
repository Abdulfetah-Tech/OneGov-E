import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { errorKey, mockNationalIdCode, problemCode, safeReturnUrl } from './auth.utils';

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

  it('reads application and workflow error types too', () => {
    expect(problemCode(new HttpErrorResponse({ status: 409, error: { type: 'application.not_editable' } }))).toBe(
      'application.not_editable',
    );
    expect(problemCode(new HttpErrorResponse({ status: 400, error: { type: 'workflow.note_required' } }))).toBe(
      'workflow.note_required',
    );
  });

  it('is unknown for anything else', () => {
    expect(problemCode(new HttpErrorResponse({ status: 500, error: 'boom' }))).toBe('unknown');
    expect(problemCode(new HttpErrorResponse({ status: 400, error: { type: 'something-else' } }))).toBe('unknown');
    expect(problemCode(new Error('x'))).toBe('unknown');
  });
});

describe('mockNationalIdCode', () => {
  it('encodes the identity the way the API mock provider expects', () => {
    expect(mockNationalIdCode('1', 'A')).toBe('mock.eyJpZE51bWJlciI6IjEiLCJmdWxsTmFtZSI6IkEifQ');
  });

  it('handles non-Latin names as UTF-8', () => {
    expect(mockNationalIdCode('1', 'አ')).toBe('mock.eyJpZE51bWJlciI6IjEiLCJmdWxsTmFtZSI6IuGKoCJ9');
  });
});

describe('errorKey', () => {
  it('maps known codes to translation keys', () => {
    expect(errorKey('identity.invalid_credentials')).toBe('error.invalid_credentials');
    expect(errorKey('rate_limited')).toBe('error.rate_limited');
    expect(errorKey('network')).toBe('error.network');
    expect(errorKey('identity.invalid_state')).toBe('error.invalid_state');
    expect(errorKey('identity.national_id_failed')).toBe('error.national_id_failed');
    expect(errorKey('application.not_editable')).toBe('error.app_not_editable');
    expect(errorKey('workflow.note_required')).toBe('error.workflow_note_required');
    expect(errorKey('workflow.invalid_state')).toBe('error.workflow_invalid_state');
  });

  it('uses the generic message for unknown codes', () => {
    expect(errorKey('identity.something_new')).toBe('error.unknown');
    expect(errorKey('unknown')).toBe('error.unknown');
    expect(errorKey('application.something_new')).toBe('error.unknown');
    expect(errorKey('workflow.something_new')).toBe('error.unknown');
  });
});
