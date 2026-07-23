import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';

/** Vælg en ny adgangskode ud fra nulstillingslinket (?token=...). */
@Component({
  selector: 'page-reset-password',
  imports: [FormsModule, RouterLink, LogoMark],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>Ny adgangskode</h1>
        </div>

        <div class="login-body">
          @if (!token()) {
            <div class="error">Linket mangler et token. Bed om et nyt nulstillingslink.</div>
            <a routerLink="/glemt-kode" class="btn-block">Bed om nyt link</a>
          } @else if (done()) {
            <div class="auth-note">Din adgangskode er nulstillet. Du kan nu logge ind med den nye kode.</div>
            <a routerLink="/login" class="btn-block">Til login</a>
          } @else {
            <form (ngSubmit)="submit()">
              <div class="field">
                <label>Ny adgangskode</label>
                <input type="password" autocomplete="new-password" [(ngModel)]="password" name="password"
                       placeholder="Vælg en ny adgangskode" />
              </div>
              <div class="field">
                <label>Gentag adgangskode</label>
                <input type="password" autocomplete="new-password" [(ngModel)]="repeat" name="repeat" />
              </div>

              @if (error()) { <div class="error">{{ error() }}</div> }

              <button class="primary" type="submit" [disabled]="loading()" style="width:100%; margin-top:.3rem">
                {{ loading() ? 'Gemmer…' : 'Gem ny adgangskode' }}
              </button>
            </form>
          }
        </div>
      </div>
    </div>
  `
})
export class ResetPasswordPage {
  private auth = inject(Auth);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  password = '';
  repeat = '';
  error = signal('');
  loading = signal(false);
  done = signal(false);
  token = signal<string | null>(this.route.snapshot.queryParamMap.get('token'));

  submit() {
    if (!this.password) { this.error.set('Skriv en ny adgangskode.'); return; }
    if (this.password !== this.repeat) { this.error.set('De to adgangskoder er ikke ens.'); return; }
    const t = this.token();
    if (!t) return;

    this.error.set('');
    this.loading.set(true);
    this.auth.resetPassword(t, this.password).subscribe({
      next: () => { this.loading.set(false); this.done.set(true); },
      error: (e) => {
        this.loading.set(false);
        this.error.set(e.error?.message ?? 'Linket er ugyldigt eller udløbet. Bed om et nyt.');
      }
    });
  }
}
