export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  tokenType: string;
}

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  language: string;
  roles: string[];
  permissions: string[];
  mfaEnabled: boolean;
  agencyId: string | null;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  language: string;
}

export interface LoginRequest {
  email: string;
  password: string;
  otpCode: string | null;
}

export interface NationalIdStatus {
  available: boolean;
  isMock: boolean;
}

export interface NationalIdStart {
  authorizationUrl: string;
  state: string;
}

export interface MfaEnrollment {
  sharedKey: string;
  otpAuthUri: string;
}

export interface MfaConfirmed {
  recoveryCodes: string[];
}
