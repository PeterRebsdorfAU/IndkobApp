import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../environments/environment';
import { AuthResult } from './models';

const API = environment.apiBase || `${location.protocol}//${location.hostname}:5298/api`;
const TOKEN_KEY = 'indkobs.token';
const REFRESH_KEY = 'indkobs.refresh';
const NAME_KEY = 'indkobs.household';
const DISPLAY_KEY = 'indkobs.display';

/** Holder styr på login-tilstand, JWT-token og den indloggede bruger (gemt i localStorage). */
@Injectable({ providedIn: 'root' })
export class Auth {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly householdName = signal<string | null>(localStorage.getItem(NAME_KEY));
  /** Den indloggede brugers viste navn (T2). Null for legacy husstands-login. */
  readonly displayName = signal<string | null>(localStorage.getItem(DISPLAY_KEY));
  readonly isLoggedIn = signal<boolean>(!!localStorage.getItem(TOKEN_KEY));

  get token(): string | null { return localStorage.getItem(TOKEN_KEY); }
  get refreshToken(): string | null { return localStorage.getItem(REFRESH_KEY); }

  login(email: string, password: string) {
    return this.http.post<AuthResult>(`${API}/auth/login`, { email, password })
      .pipe(tap(r => this.store(r)));
  }

  /**
   * Selvbetjent oprettelse. Ny husstand: angiv householdName. Join eksisterende:
   * angiv inviteToken (fra et invitationslink). Logger automatisk ind ved succes.
   */
  signup(body: {
    email: string; password: string; displayName: string;
    householdName?: string | null; inviteToken?: string | null;
  }) {
    return this.http.post<AuthResult>(`${API}/auth/signup`, body)
      .pipe(tap(r => this.store(r)));
  }

  forgotPassword(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API}/auth/forgot-password`, { email });
  }

  resetPassword(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API}/auth/reset-password`, { token, newPassword });
  }

  confirmEmail(token: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${API}/auth/confirm-email`, { token });
  }

  private store(r: AuthResult) {
    localStorage.setItem(TOKEN_KEY, r.token);
    localStorage.setItem(NAME_KEY, r.householdName);
    if (r.refreshToken) localStorage.setItem(REFRESH_KEY, r.refreshToken);
    if (r.displayName) localStorage.setItem(DISPLAY_KEY, r.displayName);
    else localStorage.removeItem(DISPLAY_KEY);
    this.householdName.set(r.householdName);
    this.displayName.set(r.displayName ?? null);
    this.isLoggedIn.set(true);
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(NAME_KEY);
    localStorage.removeItem(DISPLAY_KEY);
    this.isLoggedIn.set(false);
    this.householdName.set(null);
    this.displayName.set(null);
    this.router.navigateByUrl('/login');
  }
}
