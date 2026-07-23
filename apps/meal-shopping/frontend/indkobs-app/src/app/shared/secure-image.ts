import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

/**
 * Viser et billede fra et beskyttet API-endpoint. Fordi endpoints kræver Bearer-token
 * (som en almindelig <img src> ikke kan sende), henter vi billedet via HttpClient —
 * så auth-interceptoren sætter token på — og viser resultatet via en object-URL.
 *
 * Object-URL'en frigives igen når src ændres eller komponenten nedlægges (ingen leaks).
 */
@Component({
  selector: 'secure-image',
  template: `
    @if (url(); as u) {
      <img [src]="u" [alt]="alt()" class="recipe-image" />
    }
  `,
})
export class SecureImage {
  private http = inject(HttpClient);
  private destroyRef = inject(DestroyRef);

  /** Fuld URL til billed-endpointet. */
  src = input.required<string>();
  alt = input('');

  url = signal<string | null>(null);
  private objectUrl: string | null = null;

  constructor() {
    effect(onCleanup => {
      const src = this.src();
      this.revoke();
      this.url.set(null);

      const sub = this.http.get(src, { responseType: 'blob' }).subscribe({
        next: blob => {
          this.objectUrl = URL.createObjectURL(blob);
          this.url.set(this.objectUrl);
        },
        error: () => this.url.set(null), // fx 404 = intet billede endnu
      });

      onCleanup(() => sub.unsubscribe());
    });

    this.destroyRef.onDestroy(() => this.revoke());
  }

  private revoke() {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
