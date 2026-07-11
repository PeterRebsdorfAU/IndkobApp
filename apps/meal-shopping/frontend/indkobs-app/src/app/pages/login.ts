import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth } from '../auth';

@Component({
  selector: 'page-login',
  imports: [FormsModule],
  template: `
    <div class="login-wrap">
      <div class="card login-card">
        <h1>🛒 Madplan &amp; Indkøb</h1>
        <p class="muted">Log ind med din husstands konto.</p>

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
    .login-wrap { min-height: 80vh; display: flex; align-items: center; justify-content: center; }
    .login-card { width: 100%; max-width: 380px; }
    h1 { font-size: 1.3rem; }
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
