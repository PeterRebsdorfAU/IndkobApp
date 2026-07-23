import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';

@Component({
  selector: 'page-login',
  imports: [FormsModule, RouterLink, LogoMark],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>Madplan</h1>
          <p>Log ind med din konto.</p>
        </div>

        <div class="login-body">
          <form (ngSubmit)="submit()">
            <div class="field">
              <label>Email</label>
              <input type="email" autocapitalize="none" autocomplete="username" [(ngModel)]="email" name="email"
                     placeholder="dig@eksempel.dk" />
            </div>
            <div class="field">
              <label>Adgangskode</label>
              <input type="password" autocomplete="current-password" [(ngModel)]="password" name="password" />
            </div>

            @if (error()) { <div class="error">{{ error() }}</div> }

            <button class="primary" type="submit" [disabled]="loading()" style="width:100%; margin-top:.3rem">
              {{ loading() ? 'Logger ind…' : 'Log ind' }}
            </button>
          </form>

          <div class="login-links">
            <a routerLink="/glemt-kode">Glemt adgangskode?</a>
            <span aria-hidden="true">·</span>
            <a routerLink="/opret">Opret ny konto</a>
          </div>
        </div>
      </div>
    </div>
  `
})
export class LoginPage {
  private auth = inject(Auth);
  private router = inject(Router);

  email = '';
  password = '';
  error = signal('');
  loading = signal(false);

  submit() {
    if (!this.email.trim() || !this.password) { this.error.set('Udfyld email og adgangskode.'); return; }
    this.error.set('');
    this.loading.set(true);
    this.auth.login(this.email.trim(), this.password).subscribe({
      next: () => { this.loading.set(false); this.router.navigateByUrl('/uge'); },
      error: (e) => {
        this.loading.set(false);
        this.error.set(e.status === 401 ? 'Forkert email eller adgangskode.' : 'Kunne ikke logge ind. Prøv igen.');
      }
    });
  }
}
