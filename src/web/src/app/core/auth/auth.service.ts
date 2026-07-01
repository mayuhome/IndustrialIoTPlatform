import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, tap, throwError } from 'rxjs';
import { AuthRequest, AuthTokenResult } from './auth.models';
import {
  clearStoredAccessToken,
  clearStoredAuthUser,
  getStoredAccessToken,
  getStoredAuthUser,
  setStoredAccessToken,
  setStoredAuthUser,
  StoredAuthUser
} from './auth-session';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  readonly currentUser = signal<StoredAuthUser | null>(getStoredAuthUser());

  constructor(private readonly httpClient: HttpClient) {}

  login(request: AuthRequest): Observable<AuthTokenResult> {
    return this.httpClient.post<AuthTokenResult>('/auth/login', request).pipe(
      tap((result) => this.persistSession(result)),
      catchError((error) => throwError(() => this.toUserFacingError(error)))
    );
  }

  register(request: AuthRequest): Observable<AuthTokenResult> {
    return this.httpClient.post<AuthTokenResult>('/auth/register', request).pipe(
      tap((result) => this.persistSession(result)),
      catchError((error) => throwError(() => this.toUserFacingError(error)))
    );
  }

  logout(): void {
    clearStoredAccessToken();
    clearStoredAuthUser();
    this.currentUser.set(null);
  }

  isAuthenticated(): boolean {
    return !!getStoredAccessToken();
  }

  private persistSession(result: AuthTokenResult): void {
    setStoredAccessToken(result.accessToken);
    const user = {
      userId: result.userId,
      username: result.username,
      role: result.role,
      expiresAtUtc: result.expiresAtUtc
    } satisfies StoredAuthUser;

    setStoredAuthUser(user);
    this.currentUser.set(user);
  }

  private toUserFacingError(error: unknown): Error {
    if (!(error instanceof HttpErrorResponse)) {
      return new Error('Authentication request failed.');
    }

    const payload = error.error;
    if (typeof payload === 'string' && payload.trim()) {
      return new Error(payload);
    }

    if (typeof payload === 'object' && payload !== null) {
      const record = payload as Record<string, unknown>;
      const message = record['message'] ?? record['detail'] ?? record['title'];
      if (typeof message === 'string' && message.trim()) {
        return new Error(message);
      }
    }

    if (error.status === 0) {
      return new Error('Unable to reach API at localhost:8080. Check that the backend is running and CORS is enabled.');
    }

    if (error.status === 401) {
      return new Error('Invalid username or password.');
    }

    return new Error(`Authentication failed with status ${error.status}.`);
   }
 }
