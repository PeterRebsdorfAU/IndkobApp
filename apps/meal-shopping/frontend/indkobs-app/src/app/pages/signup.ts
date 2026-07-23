import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';
import { ConsentCheckbox } from '../shared/consent-checkbox';

/**
 * Selvbetjent oprettelse. To tilstande:
 *  - Ny husstand (standard): brugeren vælger et husstandsnavn.
 *  - Join eksisterende husstand: hvis URL'en har ?invite=<token> (fra et invitationslink),
 *    oprettes brugeren i den husstand, og husstandsnavn-feltet skjules.
 */
@Component({
  selector: 'page-signup',
  imports: [FormsModule, RouterLink, LogoMark, ConsentCheckbox],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>{{ inviteToken() ? 'Tilslut husstand' : 'Opret konto' }}</h1>
          <p>
            {{ inviteToken()
              ? 'Du er inviteret til en eksisterende husstand. Opret din egen bruger her.'
              : 'Opret en ny husstand og din personlige bruger.' }}
          </p>
        </div>

        <div class="login-body">
          <form (ngSubmit)="submit()">
            <div class="field">
              <label>Dit navn</label>
              <input type="text" autocomplete="name" [(ngModel)]="displayName" name="displayName"
                     placeholder="fx Clara" />
            </div>

            @if (!inviteToken()) {
              <div class="field">
                <label>Husstandens navn</label>
                <input type="text" [(ngModel)]="householdName" name="householdName"
                       placeholder="fx Familien Hansen" />
              </div>
            }

            <div class="field">
              <label>Email</label>
              <input type="email" autocapitalize="none" autocomplete="username" [(ngModel)]="email" name="email"
                     placeholder="dig@eksempel.dk" />
            </div>
            <div class="field">
              <label>Adgangskode</label>
              <input type="password" autocomplete="new-password" [(ngModel)]="password" name="password"
                     placeholder="Mindst 8 tegn" />
            </div>

            <consent-checkbox [(accepted)]="consent" />

            @if (error()) { <div class="error">{{ error() }}</div> }

            <button class="primary" type="submit" [disabled]="loading() || !consent()" style="width:100%; margin-top:.3rem">
              {{ loading() ? 'Opretter…' : 'Opret konto' }}
            </button>
          </form>

          <div class="login-links">
            <span class="muted">Har du allerede en konto?</span>
            <a routerLink="/login">Log ind</a>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SignupPage {
  private auth = inject(Auth);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  displayName = '';
  householdName = '';
  email = '';
  password = '';
  consent = signal(false);
  error = signal('');
  loading = signal(false);
  inviteToken = signal<string | null>(null);

  constructor() {
    const t = this.route.snapshot.queryParamMap.get('invite');
    if (t) this.inviteToken.set(t);
  }

  submit() {
    if (!this.displayName.trim()) { this.error.set('Skriv dit navn.'); return; }
    if (!this.inviteToken() && !this.householdName.trim()) { this.error.set('Angiv et husstandsnavn.'); return; }
    if (!this.email.trim()) { this.error.set('Skriv din email.'); return; }
    if (this.password.length < 8) { this.error.set('Adgangskoden skal være mindst 8 tegn.'); return; }
    if (!this.consent()) { this.error.set('Du skal acceptere betingelserne.'); return; }

    this.error.set('');
    this.loading.set(true);
    this.auth.signup({
      email: this.email.trim(),
      password: this.password,
      displayName: this.displayName.trim(),
      householdName: this.inviteToken() ? null : this.householdName.trim(),
      inviteToken: this.inviteToken()
    }).subscribe({
      next: () => { this.loading.set(false); this.router.navigateByUrl('/uge'); },
      error: (e) => {
        this.loading.set(false);
        this.error.set(e.error?.message ?? 'Kunne ikke oprette kontoen. Prøv igen.');
      }
    });
  }
}
