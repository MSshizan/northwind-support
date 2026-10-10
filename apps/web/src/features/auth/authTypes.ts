export interface AuthenticatedAgent {
  id: number;
  fullName: string;
  email: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  agent: AuthenticatedAgent;
}

export interface AuthSession extends LoginResponse {}
