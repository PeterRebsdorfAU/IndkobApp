import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';

@Component({
  selector: 'page-login',
  imports: [FormsModule, LogoMark],
  template: `
    <div class="login-wrap">
      <div class="card login-card">
        <div class="login-brand">
          <app-logo [size]="52" />
          <h1>Madplan &amp; Indkøb</h1>
          <p class="muted">Log ind med din husstands konto.</p>
        </div>

        <form (ngSubmit)="submit()">
          <div class="field">
            <label>Brugernavn</label>
            <input type="text" autocapitalize="none" autocomplete="username" [(ngModel)]="email" name="email"
                   placeholder="fx ClaraPeter" />
          </div>
          <div class="field">
            <label>Adgangskode</label>
            <input type="password" autocomplete="current-password" [(ngModel)]="password" name="password" />
          </div>

          @if (error()) { <div class="error">{{ error() }}</div> }

          <button class="primary" type="submit" [disabled]="loading()" style="width:100%">
            {{ loading() ? 'Logger ind…' : 'Log ind' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { min-height: 82vh; display: flex; align-items: center; justify-content: center; padding: 1rem 0; }
    .login-card { width: 100%; max-width: 380px; padding: 1.6rem 1.4rem; box-shadow: var(--shadow-md); }
    .login-brand { text-align: center; margin-bottom: 1.3rem; }
    .login-brand app-logo { margin-bottom: .7rem; }
    .login-brand h1 { font-size: 1.35rem; margin: 0 0 .2rem; }
    .login-brand p { margin: 0; }
  `]
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
