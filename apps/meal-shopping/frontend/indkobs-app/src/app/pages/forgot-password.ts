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
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>Glemt adgangskode</h1>
          <p>Skriv din email, så sender vi et link til at vælge en ny adgangskode.</p>
        </div>

        <div class="login-body">
          @if (sent()) {
            <div class="auth-note">
              Hvis emailen findes, har vi sendt et link til at nulstille adgangskoden.
              Tjek din indbakke (og evt. spam).
            </div>
            <a routerLink="/login" class="btn-block">Tilbage til login</a>
          } @else {
            <form (ngSubmit)="submit()">
              <div class="field">
                <label>Email</label>
                <input type="email" autocapitalize="none" autocomplete="username" [(ngModel)]="email" name="email"
                       placeholder="dig@eksempel.dk" />
              </div>

              @if (error()) { <div class="error">{{ error() }}</div> }

              <button class="primary" type="submit" [disabled]="loading()" style="width:100%; margin-top:.3rem">
                {{ loading() ? 'Sender…' : 'Send nulstillingslink' }}
              </button>
            </form>

            <div class="login-links">
              <a routerLink="/login">Tilbage til login</a>
            </div>
          }
        </div>
      </div>
    </div>
  `
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
