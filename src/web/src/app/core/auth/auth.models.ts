export interface AuthRequest {
  username: string;
  password: string;
}

export interface AuthTokenResult {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  username: string;
  role: string;
}
