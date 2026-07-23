import { Component, inject, signal } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { Auth } from '../auth';
import { LogoMark } from '../shared/logo';

/** Bekræfter brugerens email ud fra bekræftelseslinket (?token=...). Kører automatisk. */
@Component({
  selector: 'page-confirm-email',
  imports: [RouterLink, LogoMark],
  template: `
    <div class="login-wrap">
      <div class="login-card">
        <div class="login-hero">
          <span class="logo-chip"><app-logo [size]="44" /></span>
          <h1>Bekræft email</h1>
        </div>

        <div class="login-body">
          @if (state() === 'loading') {
            <p class="muted" style="text-align:center">Bekræfter din email…</p>
          } @else if (state() === 'ok') {
            <div class="auth-note">{{ message() }}</div>
          } @else {
            <div class="error">{{ message() }}</div>
          }

          <a routerLink="/uge" class="btn-block" style="margin-top:1rem">Fortsæt til appen</a>
        </div>
      </div>
    </div>
  `
})
export class ConfirmEmailPage {
  private auth = inject(Auth);
  private route = inject(ActivatedRoute);

  state = signal<'loading' | 'ok' | 'error'>('loading');
  message = signal('');

  constructor() {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.state.set('error');
      this.message.set('Bekræftelseslinket mangler et token.');
      return;
    }
    this.auth.confirmEmail(token).subscribe({
      next: (r) => { this.state.set('ok'); this.message.set(r.message ?? 'Din email er bekræftet.'); },
      error: (e) => { this.state.set('error'); this.message.set(e.error?.message ?? 'Bekræftelseslinket er ugyldigt eller udløbet.'); }
    });
  }
}
