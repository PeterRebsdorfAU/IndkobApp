import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Router } from '@angular/router';
import { resetOnboarding } from '../shared/onboarding-state';

// Support-adresse. Ét sted, så den er nem at ændre. (Skift til jeres rigtige
// support-mail før udrulning.)
const SUPPORT_EMAIL = 'support@madplan-app.dk';

/** Ét spørgsmål/svar i FAQ'en. */
interface Qa { q: string; a: string; }

/**
 * Hjælp: "Kom godt i gang", ofte stillede spørgsmål og support-link.
 * Rute: /hjaelp (tilgås fra ?-ikonet i topbjælken).
 */
@Component({
  selector: 'page-faq',
  imports: [RouterLink],
  template: `
    <h1>Hjælp &amp; FAQ</h1>
    <p class="muted">Kom godt i gang med Madplan &amp; Indkøb, og find svar på de mest almindelige spørgsmål.</p>

    <!-- Kom godt i gang -->
    <div class="card">
      <h2 style="margin-top:0">Kom godt i gang</h2>
      <ol class="steps">
        <li><b>Planlæg ugen.</b> På <a routerLink="/uge">Uge</a> vælger I dagens retter og varegrupper.</li>
        <li><b>Få indkøbslisten.</b> <a routerLink="/indkob">Indkøb</a> samler alt, omregner enheder og sorterer efter butik.</li>
        <li><b>Send til butik (valgfrit).</b> Send listen som en ordre fra indkøbssiden og følg status.</li>
      </ol>
      <button class="small" (click)="replayIntro()">▶︎ Se introduktionen igen</button>
    </div>

    <!-- FAQ -->
    <div class="card">
      <h2 style="margin-top:0">Ofte stillede spørgsmål</h2>
      @for (item of faq; track item.q) {
        <details class="faq-item">
          <summary>{{ item.q }}</summary>
          <p>{{ item.a }}</p>
        </details>
      }
    </div>

    <!-- Support -->
    <div class="card">
      <h2 style="margin-top:0">Brug for mere hjælp?</h2>
      <p class="muted">Kan du ikke finde svaret her, så skriv til os — vi vender tilbage hurtigst muligt.</p>
      <a class="support-btn" [href]="supportMailto">✉️ Kontakt support</a>
      <p class="muted" style="margin-top:.5rem">Eller send en mail til <a [href]="'mailto:' + supportEmail">{{ supportEmail }}</a>.</p>
    </div>
  `,
  styles: [`
    .steps { margin: .2rem 0 .8rem; padding-left: 1.2rem; }
    .steps li { margin-bottom: .5rem; line-height: 1.45; }
    .faq-item { border-bottom: 1px solid var(--border); padding: .2rem 0; }
    .faq-item:last-child { border-bottom: none; }
    .faq-item summary {
      cursor: pointer; padding: .6rem 0; font-weight: 600;
      list-style: none; display: flex; align-items: center; gap: .5rem;
    }
    .faq-item summary::-webkit-details-marker { display: none; }
    .faq-item summary::before { content: '＋'; color: var(--primary); font-weight: 700; }
    .faq-item[open] summary::before { content: '－'; }
    .faq-item p { margin: 0 0 .7rem 1.4rem; color: var(--muted); line-height: 1.5; }
    .support-btn {
      display: inline-block; background: var(--primary); color: #fff;
      padding: .6rem .9rem; border-radius: 10px; text-decoration: none; font-weight: 600;
      min-height: 44px; line-height: 1.9;
    }
  `]
})
export class FaqPage {
  private router = inject(Router);

  readonly supportEmail = SUPPORT_EMAIL;
  readonly supportMailto =
    `mailto:${SUPPORT_EMAIL}?subject=${encodeURIComponent('Hjælp til Madplan & Indkøb')}`;

  readonly faq: Qa[] = [
    {
      q: 'Hvordan laver jeg en indkøbsliste?',
      a: 'Vælg en uge på Uge-fanen og tilføj retter og varegrupper. Gå derefter til Indkøb — ' +
         'listen bygges automatisk ud fra ugens plan.'
    },
    {
      q: 'Hvordan lægger appen ens varer sammen?',
      a: 'Samme vare summeres, og enheder omregnes hvor det giver mening (g↔kg, ml↔l). ' +
         'Varer i enheder der ikke kan omregnes (fx stk og pakke) vises på hver sin linje.'
    },
    {
      q: 'Kan jeg dele indkøbslisten med andre?',
      a: 'Ja. På indkøbssiden kan I lave et delings-link. Modtageren kan se og krydse af uden at ' +
         'logge ind, og afkrydsninger opdateres for jer begge.'
    },
    {
      q: 'Hvad er varegrupper?',
      a: 'Faste sæt af varer I køber igen og igen — fx "Morgenmad" eller "Rengøring". ' +
         'Læg en varegruppe på ugen, så ryger alle dens varer med på listen.'
    },
    {
      q: 'Hvordan sender jeg listen til en butik?',
      a: 'Hvis din butik er tilknyttet, kan du sende hele ugens indkøbsliste som en ordre fra ' +
         'indkøbssiden og følge status fra Modtaget til Afhentet under "Mine ordrer".'
    },
    {
      q: 'Forsvinder mine gamle uger?',
      a: 'Uger ældre end 5 uger ryddes automatisk væk for at holde det overskueligt. ' +
         'Jeres retter og varegrupper røres ikke.'
    },
    {
      q: 'Virker appen offline?',
      a: 'Madplan & Indkøb er en PWA og kan installeres på telefonen. Meget virker offline, men ' +
         'ændringer synkroniseres når du er online igen.'
    }
  ];

  replayIntro() {
    resetOnboarding();
    this.router.navigateByUrl('/velkommen');
  }
}
