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
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>Madplan</h1>
          <p>Log ind med din husstands konto.</p>
        </div>

        <form class="login-form" (ngSubmit)="submit()">
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

          <button class="primary" type="submit" [disabled]="loading()" style="width:100%; margin-top:.3rem">
            {{ loading() ? 'Logger ind…' : 'Log ind' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { min-height: 86vh; display: flex; align-items: center; justify-content: center; padding: 1.2rem 0; }
    .login-card {
      width: 100%; max-width: 392px; background: var(--surface);
      border: 1px solid var(--line); border-radius: 28px; overflow: hidden; box-shadow: var(--shadow-lift);
    }
    .login-hero {
      text-align: center; color: #fff; padding: 2rem 1.6rem 1.6rem;
      background: radial-gradient(130% 150% at 100% 0%, #1a8f60 0%, #0c5638 58%, #0a4a30 100%);
    }
    .logo-chip {
      display: inline-flex; background: #fff; padding: 10px; border-radius: 18px;
      box-shadow: 0 12px 26px -12px rgba(0,0,0,.45);
    }
    .login-hero h1 { color: #fff; font-size: 1.9rem; margin: .8rem 0 .25rem; }
    .login-hero p { color: rgba(255,255,255,.82); margin: 0; font-size: .92rem; }
    .login-form { padding: 1.5rem 1.6rem 1.7rem; }
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
