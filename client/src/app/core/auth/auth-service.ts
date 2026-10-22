import { HttpClient } from '@angular/common/http';
import { Service, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map, tap } from 'rxjs';

// Response of POST /api/identity/login (Application/Abstractions/Identity/TokenResponse)
export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

// What is kept in localStorage so a page refresh does not log the user out
interface Session {
  email: string;
  accessToken: string;
  expiresAt: string;
}

const STORAGE_KEY = 'depot.session';

@Service()
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // Signal so the sidebar (email, logout) updates as soon as the session changes
  private readonly session = signal<Session | null>(readStoredSession());

  readonly email = computed(() => this.session()?.email ?? '');

  // Token for the interceptor; null once it has expired so we never send a stale token
  accessToken(): string | null {
    const session = this.session();
    return session && !isExpired(session) ? session.accessToken : null;
  }

  // Checked by the route guards on every navigation (time-based, so not a computed signal)
  isLoggedIn(): boolean {
    return this.accessToken() !== null;
  }

  login(email: string, password: string): Observable<void> {
    return this.http.post<TokenResponse>('/api/identity/login', { email, password }).pipe(
      tap((response) => this.store({ email, accessToken: response.accessToken, expiresAt: response.expiresAt })),
      map(() => undefined),
    );
  }

  // expired = true when the API answered 401: keep the current page so the user returns to it after login
  logout(expired = false): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    const returnUrl = expired ? this.router.url : undefined;
    void this.router.navigate(['/login'], { queryParams: returnUrl ? { returnUrl } : {} });
  }

  private store(session: Session): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }
}

function isExpired(session: Session): boolean {
  return new Date(session.expiresAt).getTime() <= Date.now();
}

// Reads the saved session; drops it when it is unreadable or already expired
function readStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    return session.accessToken && !isExpired(session) ? session : null;
  } catch {
    return null;
  }
}
