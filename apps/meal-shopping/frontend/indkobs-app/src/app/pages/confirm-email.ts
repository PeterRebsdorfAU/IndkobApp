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
      <div class="card login-card">
        <div class="login-brand">
          <app-logo [size]="52" />
          <h1>Bekræft email</h1>
        </div>

        @if (state() === 'loading') {
          <p class="muted" style="text-align:center">Bekræfter din email…</p>
        } @else if (state() === 'ok') {
          <div class="success">{{ message() }}</div>
        } @else {
          <div class="error">{{ message() }}</div>
        }

        <a routerLink="/uge" class="primary" style="width:100%;display:block;text-align:center;margin-top:1rem">Fortsæt til appen</a>
      </div>
    </div>
  `,
  styles: [`
    .login-wrap { min-height: 82vh; display: flex; align-items: center; justify-content: center; padding: 1rem 0; }
    .login-card { width: 100%; max-width: 400px; padding: 1.6rem 1.4rem; box-shadow: var(--shadow-md); }
    .login-brand { text-align: center; margin-bottom: 1.3rem; }
    .login-brand app-logo { margin-bottom: .7rem; }
    .login-brand h1 { font-size: 1.35rem; margin: 0; }
    .success { background: var(--surface-2, #eef7ee); border-radius: var(--radius, 8px); padding: .8rem 1rem; font-size: .92rem; }
  `]
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
