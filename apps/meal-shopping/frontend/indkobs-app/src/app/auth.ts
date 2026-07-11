import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../environments/environment';
import { AuthResult } from './models';

const API = environment.apiBase || `${location.protocol}//${location.hostname}:5298/api`;
const TOKEN_KEY = 'indkobs.token';
const NAME_KEY = 'indkobs.household';

/** Holder styr på login-tilstand og JWT-token (gemt i localStorage). */
@Injectable({ providedIn: 'root' })
export class Auth {
  private http = inject(HttpClient);
  private router = inject(Router);

  readonly householdName = signal<string | null>(localStorage.getItem(NAME_KEY));
  readonly isLoggedIn = signal<boolean>(!!localStorage.getItem(TOKEN_KEY));

  get token(): string | null { return localStorage.getItem(TOKEN_KEY); }

  login(email: string, password: string) {
    return this.http.post<AuthResult>(`${API}/auth/login`, { email, password }).pipe(
      tap(r => {
        localStorage.setItem(TOKEN_KEY, r.token);
        localStorage.setItem(NAME_KEY, r.householdName);
        this.householdName.set(r.householdName);
        this.isLoggedIn.set(true);
      })
    );
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(NAME_KEY);
    this.isLoggedIn.set(false);
    this.householdName.set(null);
    this.router.navigateByUrl('/login');
  }
}
