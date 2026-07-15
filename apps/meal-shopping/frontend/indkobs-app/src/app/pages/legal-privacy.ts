import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { Api } from '../api';
import { Auth } from '../auth';

/**
 * Privatlivspolitik + kort cookie/samtykke-note + selvbetjening (GDPR):
 * data-eksport og permanent sletning af egen husstand. Indholdsdelen er
 * tilgængelig uden login; selvbetjenings-knapperne vises kun når man er logget ind.
 * NB: Oplysninger i [firkantede parenteser] skal udfyldes af den dataansvarlige.
 */
@Component({
  selector: 'page-legal-privacy',
  imports: [FormsModule, RouterLink],
  template: `
    <article class="card legal">
      <a routerLink="/uge" class="btn-link">&larr; Tilbage</a>
      <h1>Privatlivspolitik</h1>
      <p class="muted">Senest opdateret: 15. juli 2026</p>

      <h2>1. Dataansvarlig</h2>
      <p>
        Dataansvarlig for behandlingen af dine personoplysninger i Madplan &amp; Indkøb er
        <strong>[udbyderens navn]</strong>, [adresse], CVR [nr.].
        Spørgsmål om databeskyttelse: <a href="mailto:[kontakt-email]">[kontakt-email]</a>.
      </p>

      <h2>2. Hvilke oplysninger behandler vi?</h2>
      <ul>
        <li><strong>Kontooplysninger:</strong> husstandsnavn og brugernavn (login) samt en krypteret (hashet) adgangskode.</li>
        <li><strong>Indhold du selv opretter:</strong> opskrifter, varegrupper, ugeplaner, indkøbslister,
          køkkenlager, opgaver og evt. ordrer.</li>
        <li><strong>Tekniske data:</strong> nødvendige server-logs (fx tidspunkt og fejl) til drift og sikkerhed.</li>
      </ul>
      <p>Vi indsamler <strong>ikke</strong> særlige kategorier af følsomme oplysninger, og vi sælger ikke data.</p>

      <h2>3. Formål og retsgrundlag</h2>
      <p>
        Vi behandler oplysningerne for at levere tjenesten (opfyldelse af aftale, GDPR art. 6, stk. 1, litra b)
        og for at kunne drive og sikre den (legitim interesse, art. 6, stk. 1, litra f). Eventuel betaling
        behandles for at opfylde købsaftalen.
      </p>

      <h2>4. Deling og databehandlere</h2>
      <p>
        Vi deler ikke dine data med tredjeparter til markedsføring. Vi bruger databehandlere til hosting og drift:
      </p>
      <ul>
        <li><strong>Neon</strong> (database) — data lagres i EU (Frankfurt).</li>
        <li><strong>Render</strong> (server-hosting) — driftet i EU (Frankfurt).</li>
      </ul>
      <p>Der er indgået databehandleraftaler med disse leverandører (se vores interne GDPR-dokumentation).</p>

      <h2>5. Opbevaring</h2>
      <p>
        Vi opbevarer dine data, så længe din husstandskonto er aktiv. Ugeplaner ældre end ca. 5 uger ryddes
        automatisk. Sletter du din konto, fjernes alt tilhørende data permanent (se afsnit 7).
      </p>

      <h2>6. Dine rettigheder</h2>
      <p>
        Du har ret til indsigt, berigtigelse, sletning, begrænsning og dataportabilitet samt til at gøre indsigelse.
        Du kan udøve retten til indsigt/portabilitet og sletning direkte i appen (se nedenfor). Du kan klage til
        <strong>Datatilsynet</strong> (datatilsynet.dk).
      </p>

      <h2>7. Cookies og samtykke</h2>
      <p>
        Appen bruger <strong>kun teknisk nødvendig lagring</strong> i din browser (localStorage) til at holde dig
        logget ind. Vi bruger <strong>ikke</strong> tracking- eller marketing-cookies og deler ikke data med
        annoncenetværk. Derfor kræves der ikke et cookie-samtykkebanner. Ved oprettelse af konto beder vi om dit
        samtykke til denne privatlivspolitik og <a routerLink="/handelsbetingelser">handelsbetingelserne</a>.
      </p>

      @if (auth.isLoggedIn()) {
        <h2>8. Selvbetjening: dine data</h2>

        <div class="selfservice">
          <h3>Download mine data</h3>
          <p class="muted">Hent alt din husstands data som en JSON-fil (ret til dataportabilitet).</p>
          @if (exportError()) { <div class="error">{{ exportError() }}</div> }
          <button class="primary" (click)="exportData()" [disabled]="exporting()">
            {{ exporting() ? 'Henter…' : 'Download mine data (JSON)' }}
          </button>
        </div>

        <div class="selfservice danger-zone">
          <h3>Slet min husstand</h3>
          <p class="muted">
            Sletter husstandskontoen og <strong>alt</strong> tilhørende data permanent — opskrifter, uger,
            lister, lager, opgaver og ordrer. Handlingen kan <strong>ikke</strong> fortrydes.
          </p>
          @if (!confirming()) {
            <button class="danger-btn" (click)="confirming.set(true)">Slet min husstand…</button>
          } @else {
            <div class="field">
              <label>Bekræft med din adgangskode</label>
              <input type="password" autocomplete="current-password"
                     [(ngModel)]="password" name="delpw" placeholder="Adgangskode" />
            </div>
            @if (deleteError()) { <div class="error">{{ deleteError() }}</div> }
            <div class="row-wrap">
              <button class="danger-btn" (click)="deleteAccount()" [disabled]="deleting()">
                {{ deleting() ? 'Sletter…' : 'Ja, slet permanent' }}
              </button>
              <button class="small" (click)="cancelDelete()" [disabled]="deleting()">Annullér</button>
            </div>
          }
        </div>
      }

      <p class="muted" style="margin-top:1.25rem">
        Se også vores <a routerLink="/handelsbetingelser">Handelsbetingelser</a>.
      </p>
    </article>
  `,
  styles: [`
    .legal { max-width: 720px; line-height: 1.55; }
    .legal h2 { margin-top: 1.25rem; }
    .legal h3 { margin-bottom: .25rem; }
    .legal p { margin: .4rem 0; }
    .legal ul { margin: .4rem 0; padding-left: 1.2rem; }
    .btn-link { display: inline-block; margin-bottom: .5rem; }
    .selfservice { border: 1px solid var(--border); border-radius: var(--radius); padding: .9rem; margin-top: .75rem; }
    .danger-zone { border-color: var(--danger); }
    .danger-btn { background: var(--danger); color: #fff; }
  `]
})
export class LegalPrivacyPage {
  private api = inject(Api);
  auth = inject(Auth);
  private router = inject(Router);

  exporting = signal(false);
  exportError = signal('');
  confirming = signal(false);
  deleting = signal(false);
  deleteError = signal('');
  password = '';

  exportData() {
    this.exportError.set('');
    this.exporting.set(true);
    this.api.exportMyData().subscribe({
      next: (data) => {
        this.exporting.set(false);
        const json = JSON.stringify(data, null, 2);
        const blob = new Blob([json], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `mine-data-${new Date().toISOString().slice(0, 10)}.json`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.exporting.set(false);
        this.exportError.set('Kunne ikke hente dine data. Prøv igen.');
      }
    });
  }

  cancelDelete() {
    this.confirming.set(false);
    this.password = '';
    this.deleteError.set('');
  }

  deleteAccount() {
    if (!this.password) { this.deleteError.set('Indtast din adgangskode for at bekræfte.'); return; }
    this.deleteError.set('');
    this.deleting.set(true);
    this.api.deleteMyHousehold(this.password).subscribe({
      next: () => {
        this.deleting.set(false);
        this.auth.logout(); // rydder token og sender til /login
      },
      error: (e) => {
        this.deleting.set(false);
        this.deleteError.set(e.status === 400
          ? 'Forkert adgangskode. Sletning blev ikke gennemført.'
          : 'Kunne ikke slette kontoen. Prøv igen.');
      }
    });
  }
}
