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
      <div class="card login-card">
        <div class="login-brand">
          <app-logo [size]="52" />
          <h1>Ny adgangskode</h1>
        </div>

        @if (!token()) {
          <div class="error">Linket mangler et token. Bed om et nyt nulstillingslink.</div>
          <a routerLink="/glemt-kode" class="primary" style="width:100%;display:block;text-align:center">Bed om nyt link</a>
        } @else if (done()) {
          <div class="success">Din adgangskode er nulstillet. Du kan nu logge ind med den nye kode.</div>
          <a routerLink="/login" class="primary" style="width:100%;display:block;text-align:center">Til login</a>
        } @else {
          <form (ngSubmit)="submit()">
            <div class="field">
              <label>Ny adgangskode</label>
              <input type="password" autocomplete="new-password" [(ngModel)]="password" name="password"
                     placeholder="Mindst 8 tegn" />
            </div>
            <div class="field">
              <label>Gentag adgangskode</label>
              <input type="password" autocomplete="new-password" [(ngModel)]="repeat" name="repeat" />
            </div>

            @if (error()) { <div class="error">{{ error() }}</div> }

            <button class="primary" type="submit" [disabled]="loading()" style="width:100%">
              {{ loading() ? 'Gemmer…' : 'Gem ny adgangskode' }}
            </button>
          </form>
        }
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { min-height: 82vh; display: flex; align-items: center; justify-content: center; padding: 1rem 0; }
    .login-card { width: 100%; max-width: 400px; padding: 1.6rem 1.4rem; box-shadow: var(--shadow-md); }
    .login-brand { text-align: center; margin-bottom: 1.3rem; }
    .login-brand app-logo { margin-bottom: .7rem; }
    .login-brand h1 { font-size: 1.35rem; margin: 0; }
    .success { background: var(--surface-2, #eef7ee); border-radius: var(--radius, 8px); padding: .8rem 1rem; margin-bottom: 1rem; font-size: .92rem; }
  `]
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
    if (this.password.length < 8) { this.error.set('Adgangskoden skal være mindst 8 tegn.'); return; }
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
