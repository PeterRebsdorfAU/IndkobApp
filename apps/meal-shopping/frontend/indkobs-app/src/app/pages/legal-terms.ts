import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Handelsbetingelser (brugervilkår) for Madplan & Indkøb.
 * Statisk indholdsside — tilgængelig både før og efter login.
 * NB: Firma-/kontaktoplysninger i [firkantede parenteser] skal udfyldes af ejeren.
 */
@Component({
  selector: 'page-legal-terms',
  imports: [RouterLink],
  template: `
    <article class="card legal">
      <a routerLink="/uge" class="btn-link">&larr; Tilbage</a>
      <h1>Handelsbetingelser</h1>
      <p class="muted">Senest opdateret: 15. juli 2026</p>

      <h2>1. Om tjenesten</h2>
      <p>
        Madplan &amp; Indkøb ("tjenesten") er en webapp, der hjælper en husstand med at planlægge
        ugens måltider og samle en fælles indkøbsliste. Tjenesten udbydes af
        <strong>[udbyderens navn]</strong>, [adresse], CVR [nr.], kontakt
        <a href="mailto:[kontakt-email]">[kontakt-email]</a>.
      </p>

      <h2>2. Konto og adgang</h2>
      <p>
        Adgang kræver en husstandskonto. Du er ansvarlig for at holde din adgangskode fortrolig og for
        aktivitet på kontoen. Kontoen deles bevidst af husstandens medlemmer.
      </p>

      <h2>3. Acceptabel brug</h2>
      <p>
        Du må ikke bruge tjenesten til ulovlige formål, forsøge at få adgang til andre husstandes data
        eller belaste tjenesten unødigt. Vi kan lukke konti, der misbruger tjenesten.
      </p>

      <h2>4. Pris og betaling</h2>
      <p>
        Tjenesten kan udbydes gratis eller mod betaling. Er der betaling, oplyses pris, betalingsperiode
        og opsigelsesvilkår klart inden køb. [Tilføj konkrete abonnementsvilkår, når betaling indføres.]
      </p>

      <h2>5. Fortrydelsesret</h2>
      <p>
        Er du forbruger, har du som udgangspunkt 14 dages fortrydelsesret ved køb af digitale tjenester.
        Fortrydelsesretten kan bortfalde, hvis du udtrykkeligt beder om at få adgang til tjenesten straks
        og anerkender, at fortrydelsesretten dermed ophører.
      </p>

      <h2>6. Tilgængelighed og ansvar</h2>
      <p>
        Vi tilstræber høj oppetid, men tjenesten leveres "som den er og forefindes" uden garanti for
        uafbrudt drift. Vi er ikke ansvarlige for indirekte tab, herunder tabt data ved forkert brug.
        Tag selv en kopi af vigtige data via data-eksporten (se <a routerLink="/privatliv">Privatlivspolitik</a>).
      </p>

      <h2>7. Opsigelse og sletning</h2>
      <p>
        Du kan til enhver tid slette din husstandskonto og alt tilhørende data direkte i appen
        (se <a routerLink="/privatliv">Privatlivspolitik</a> &rarr; "Slet min husstand"). Sletning er permanent.
      </p>

      <h2>8. Ændringer</h2>
      <p>
        Vi kan opdatere disse betingelser. Væsentlige ændringer varsles i appen. Fortsat brug efter en
        ændring betragtes som accept.
      </p>

      <h2>9. Lovvalg og tvister</h2>
      <p>
        Betingelserne er underlagt dansk ret. Tvister afgøres ved de danske domstole. Forbrugere kan klage
        via Konkurrence- og Forbrugerstyrelsens Center for Klageløsning samt EU-Kommissionens
        online tvistbilæggelsesplatform.
      </p>

      <p class="muted">Se også vores <a routerLink="/privatliv">Privatlivspolitik</a>.</p>
    </article>
  `,
  styles: [`
    .legal { max-width: 720px; line-height: 1.55; }
    .legal h2 { margin-top: 1.25rem; }
    .legal p { margin: .4rem 0; }
    .btn-link { display: inline-block; margin-bottom: .5rem; }
  `]
})
export class LegalTermsPage {}
