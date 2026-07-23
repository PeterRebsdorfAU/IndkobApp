import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';

/** Bed om et link til at nulstille adgangskoden. Svaret afslører ikke om emailen findes. */
@Component({
  selector: 'page-forgot-password',
  imports: [FormsModule, RouterLink, LogoMark],
  template: `
    <div class="login-wrap">
      <div class="card login-card">
        <div class="login-brand">
          <app-logo [size]="52" />
          <h1>Glemt adgangskode</h1>
          <p class="muted">Skriv din email, så sender vi et link til at vælge en ny adgangskode.</p>
        </div>

        @if (sent()) {
          <div class="success">
            Hvis emailen findes, har vi sendt et link til at nulstille adgangskoden.
            Tjek din indbakke (og evt. spam).
          </div>
          <a routerLink="/login" class="primary" style="width:100%;display:block;text-align:center">Tilbage til login</a>
        } @else {
          <form (ngSubmit)="submit()">
            <div class="field">
              <label>Email</label>
              <input type="email" autocapitalize="none" autocomplete="username" [(ngModel)]="email" name="email"
                     placeholder="dig@eksempel.dk" />
            </div>

            @if (error()) { <div class="error">{{ error() }}</div> }

            <button class="primary" type="submit" [disabled]="loading()" style="width:100%">
              {{ loading() ? 'Sender…' : 'Send nulstillingslink' }}
            </button>
          </form>

          <div class="login-links">
            <a routerLink="/login">Tilbage til login</a>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { min-height: 82vh; display: flex; align-items: center; justify-content: center; padding: 1rem 0; }
    .login-card { width: 100%; max-width: 400px; padding: 1.6rem 1.4rem; box-shadow: var(--shadow-md); }
    .login-brand { text-align: center; margin-bottom: 1.3rem; }
    .login-brand app-logo { margin-bottom: .7rem; }
    .login-brand h1 { font-size: 1.35rem; margin: 0 0 .2rem; }
    .login-brand p { margin: 0; }
    .login-links { margin-top: 1.1rem; text-align: center; font-size: .9rem; }
    .success { background: var(--surface-2, #eef7ee); border-radius: var(--radius, 8px); padding: .8rem 1rem; margin-bottom: 1rem; font-size: .92rem; }
  `]
})
export class ForgotPasswordPage {
  private auth = inject(Auth);

  email = '';
  error = signal('');
  loading = signal(false);
  sent = signal(false);

  submit() {
    if (!this.email.trim()) { this.error.set('Skriv din email.'); return; }
    this.error.set('');
    this.loading.set(true);
    this.auth.forgotPassword(this.email.trim()).subscribe({
      next: () => { this.loading.set(false); this.sent.set(true); },
      error: () => {
        // Vis samme kvittering ved fejl for ikke at afsløre gyldige emails / lække detaljer.
        this.loading.set(false); this.sent.set(true);
      }
    });
  }
}
