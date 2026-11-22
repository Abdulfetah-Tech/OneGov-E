import { HttpErrorResponse } from '@angular/common/http';

const FALLBACK_URL = '/account';

/** Only same-site paths are accepted as a return address, so a crafted link cannot send people to another site. */
export function safeReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
    return FALLBACK_URL;
  }
  return value;
}

const KNOWN_CODES = new Set([
  'invalid_credentials',
  'invalid_otp',
  'email_taken',
  'weak_password',
  'invalid_request',
  'reauthentication_failed',
  'mfa_already_enabled',
  'mfa_not_started',
  'invalid_state',
  'national_id_failed',
  'national_id_unavailable',
  'rate_limited',
  'network',
]);

/**
 * The "authorization code" the development mock provider returns: the identity, base64url-encoded. Must match
 * MockNationalIdProvider on the API. Development only; the real provider returns an opaque code.
 */
export function mockNationalIdCode(idNumber: string, fullName: string): string {
  const bytes = new TextEncoder().encode(JSON.stringify({ idNumber, fullName }));
  let binary = '';
  bytes.forEach((byte) => (binary += String.fromCharCode(byte)));
  return 'mock.' + btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Reduces a failed HTTP call to a short code: the API's `identity.*` error type, or rate_limited, network or unknown. */
export function problemCode(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'unknown';
  }
  if (error.status === 0) {
    return 'network';
  }
  if (error.status === 429) {
    return 'rate_limited';
  }
  const type: unknown = error.error?.type;
  return typeof type === 'string' && type.startsWith('identity.') ? type : 'unknown';
}

/** Translation key for an error code. Anything not recognised falls back to the generic message. */
export function errorKey(code: string): string {
  const name = code.startsWith('identity.') ? code.slice('identity.'.length) : code;
  return KNOWN_CODES.has(name) ? `error.${name}` : 'error.unknown';
}
