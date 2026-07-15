import { Component, input } from '@angular/core';

/**
 * Genbrugelig "tom tilstand": et venligt ikon, en overskrift, en kort forklaring
 * og plads til en handling (projiceret via <ng-content>, fx en knap eller et link).
 *
 * Bruger appens design-tokens (var(--...)) så den passer ind — også når T1's
 * design system lander. Al styling er scoped til komponenten, så vi rører ikke
 * den globale styles.scss.
 */
@Component({
  selector: 'app-empty-state',
  template: `
    <div class="empty-state">
      @if (icon()) { <div class="es-icon" aria-hidden="true">{{ icon() }}</div> }
      <h2 class="es-title">{{ title() }}</h2>
      @if (text()) { <p class="es-text">{{ text() }}</p> }
      <div class="es-actions"><ng-content /></div>
    </div>
  `,
  styles: [`
    .empty-state {
      text-align: center;
      color: var(--muted);
      padding: 2.5rem 1rem;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: .5rem;
    }
    .es-icon {
      font-size: 2.6rem;
      line-height: 1;
      opacity: .9;
    }
    .es-title {
      color: var(--text);
      font-size: 1.05rem;
      font-weight: 600;
      margin: .2rem 0 0;
    }
    .es-text {
      margin: 0;
      max-width: 34ch;
      font-size: .9rem;
      line-height: 1.45;
    }
    .es-actions {
      margin-top: .6rem;
      display: flex;
      flex-wrap: wrap;
      gap: .5rem;
      justify-content: center;
    }
    /* Skjul handlings-området helt hvis intet projiceres */
    .es-actions:empty { display: none; margin: 0; }
  `]
})
export class EmptyState {
  /** Emoji/ikon øverst (valgfrit). */
  icon = input('');
  /** Kort overskrift. */
  title = input.required<string>();
  /** Forklarende brødtekst (valgfri). */
  text = input('');
}
