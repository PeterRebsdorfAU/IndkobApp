import { Component, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
  imports: [FormsModule, RouterLink],
  template: `
    <label class="consent">
      <input type="checkbox" [(ngModel)]="accepted" name="consent" />
      <span>
        Jeg accepterer
        <a routerLink="/handelsbetingelser" target="_blank">handelsbetingelserne</a>
        og
        <a routerLink="/privatliv" target="_blank">privatlivspolitikken</a>.
      </span>
    </label>
  `,
  styles: [`
    .consent { display: flex; gap: .5rem; align-items: flex-start; font-size: .9rem; color: var(--text); margin: .5rem 0; }
    .consent input { width: auto; margin-top: .15rem; }
  `]
})
export class ConsentCheckbox {
  /** Tovejs-bundet: sand når brugeren har accepteret. */
  readonly accepted = model(false);
}
