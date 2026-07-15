import { Component, inject, Injectable, signal } from '@angular/core';

export type ToastKind = 'default' | 'success' | 'error';
export interface Toast { id: number; text: string; kind: ToastKind; }

/**
 * Diskret toast/snackbar. Kald `toast.success('…')` / `toast.error('…')`
 * fra en hvilken som helst side; beskeden vises kort over bundnavigationen.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private seq = 0;

  show(text: string, kind: ToastKind = 'default', ms = 2800) {
    const id = ++this.seq;
    this.toasts.update(t => [...t, { id, text, kind }]);
    setTimeout(() => this.dismiss(id), ms);
  }
  success(text: string) { this.show(text, 'success'); }
  error(text: string) { this.show(text, 'error'); }
  dismiss(id: number) { this.toasts.update(t => t.filter(x => x.id !== id)); }
}

/** Host der viser de aktive toasts. Placeres én gang i app-shell'en. */
@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toast-host" aria-live="polite" aria-atomic="true">
      @for (t of toast.toasts(); track t.id) {
        <div class="toast" [class.success]="t.kind === 'success'" [class.error]="t.kind === 'error'"
             role="status" (click)="toast.dismiss(t.id)">
          <span class="toast-ic">
            @if (t.kind === 'error') {
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            } @else {
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M20 6 9 17l-5-5" />
              </svg>
            }
          </span>
          <span class="grow">{{ t.text }}</span>
        </div>
      }
    </div>
  `,
})
export class ToastHost {
  toast = inject(ToastService);
}
