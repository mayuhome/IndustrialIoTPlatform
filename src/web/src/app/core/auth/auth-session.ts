const ACCESS_TOKEN_STORAGE_KEY = 'access_token';
const AUTH_USER_STORAGE_KEY = 'auth_user';

export interface StoredAuthUser {
  userId: string;
  username: string;
  role: string;
  expiresAtUtc: string;
}

export function getStoredAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY) ?? localStorage.getItem('token');
}

export function setStoredAccessToken(token: string): void {
  localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token);
  localStorage.setItem('token', token);
}

export function clearStoredAccessToken(): void {
  localStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
  localStorage.removeItem('token');
}

export function setStoredAuthUser(user: StoredAuthUser): void {
  localStorage.setItem(AUTH_USER_STORAGE_KEY, JSON.stringify(user));
}

export function getStoredAuthUser(): StoredAuthUser | null {
  const raw = localStorage.getItem(AUTH_USER_STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as StoredAuthUser;
  } catch {
    localStorage.removeItem(AUTH_USER_STORAGE_KEY);
    return null;
  }
}

export function clearStoredAuthUser(): void {
  localStorage.removeItem(AUTH_USER_STORAGE_KEY);
}
