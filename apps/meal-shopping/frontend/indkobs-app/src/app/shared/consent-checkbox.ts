import { Component, model } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Genbrugelig samtykke-checkbox til signup (GDPR).
 *
 * STUB til T2 (individuelle brugerkonti + signup): T2's signup-side skal importere
 * denne komponent, tovejs-binde `accepted` og blokere oprettelse indtil den er sat.
 * Eksempel:
 *   <consent-checkbox [(accepted)]="consent" />
 *   <button [disabled]="!consent()" (click)="signup()">Opret</button>
 *
 * Placeres her (shared/) så både signup og evt. andre samtykke-punkter kan genbruge den
 * uden at afhænge af, at T2 er merget endnu.
 */
@Component({
  selector: 'consent-checkbox',
  imports: [RouterLink],
  template: `
    <label class="consent">
      <input type="checkbox" [checked]="accepted()" (change)="onToggle($event)" name="consent" />
      <span>
        Jeg accepterer
        <a routerLink="/handelsbetingelser" target="_blank" (click)="$event.stopPropagation()">handelsbetingelserne</a>
        og
        <a routerLink="/privatliv" target="_blank" (click)="$event.stopPropagation()">privatlivspolitikken</a>.
      </span>
    </label>
  `,
  styles: [`
    .consent { display: flex; gap: .55rem; align-items: flex-start; font-size: .9rem; color: var(--text); margin: .6rem 0; cursor: pointer; }
    .consent input { width: auto; margin-top: .15rem; flex: 0 0 auto; width: 20px; height: 20px; cursor: pointer; accent-color: var(--primary); }
    .consent a { font-weight: 600; }
  `]
})
export class ConsentCheckbox {
  /** Tovejs-bundet: sand når brugeren har accepteret. */
  readonly accepted = model(false);

  onToggle(e: Event) { this.accepted.set((e.target as HTMLInputElement).checked); }
}
