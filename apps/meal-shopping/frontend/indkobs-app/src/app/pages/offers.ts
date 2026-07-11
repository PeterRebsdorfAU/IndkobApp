import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api } from '../api';
import { WeekState } from '../shared/week-state';
import { Offer, OfferMatch } from '../models';

/**
 * Tilbudsside: matcher ugens indkøbsliste mod danske dagligvaretilbud
 * (Tilbudsdata.dk) + fri søgning. Kræver API-adgang konfigureret på serveren —
 * ellers vises en venlig vejledning.
 */
@Component({
  selector: 'page-offers',
  imports: [FormsModule, RouterLink],
  template: `
    <h1>💰 Tilbud</h1>

    @if (configured() === false) {
      <div class="card">
        <h3>Tilbuds-integrationen er ikke aktiveret endnu</h3>
        <p class="muted">
          Siden bruger <b>Tilbudsdata.dk</b> (dansk API med dagligvaretilbud). Adgang kræver en
          gratis-at-spørge API-nøgle:
        </p>
        <ol class="muted" style="padding-left:1.2rem">
          <li>Skriv til <b>support&#64;effectmanager.com</b> og bed om API-adgang til api.tilbudsdata.dk (bruger-ID + nøgle).</li>
          <li>Sæt to miljøvariabler på backend-servicen i Render:
            <code>Tilbudsdata__UserId</code> og <code>Tilbudsdata__ApiKey</code>.</li>
          <li>Redeploy — så vågner denne side op af sig selv. ✨</li>
        </ol>
      </div>
    } @else if (configured() === true) {
      <!-- Match mod ugens liste -->
      <div class="card">
        <div class="spread">
          <div class="grow">
            <h3>Tilbud på din indkøbsliste</h3>
            <div class="muted">Søger tilbud for hver vare du mangler at købe.</div>
          </div>
          <button class="primary small" (click)="match()" [disabled]="matching() || !hasWeek()">
            {{ matching() ? 'Søger…' : 'Find tilbud' }}
          </button>
        </div>
        @if (!hasWeek()) { <p class="muted">Vælg først en uge på <a routerLink="/uge">Uge-fanen</a>.</p> }
        @if (matchedOnce() && matches().length === 0 && !matching()) {
          <p class="muted">Ingen tilbud fundet på listens varer lige nu.</p>
        }
      </div>

      @for (m of matches(); track m.itemName) {
        <div class="card">
          <h3>{{ m.itemName }}</h3>
          @for (o of m.offers; track $index) {
            <div class="list-item">
              <div class="grow">
                <div>{{ o.heading }}</div>
                @if (o.description) { <div class="muted">{{ o.description }}</div> }
              </div>
              <div style="text-align:right">
                @if (o.price != null) { <div><b>{{ o.price }} kr</b></div> }
                @if (o.store) { <div class="muted" style="font-size:.75rem">{{ o.store }}</div> }
              </div>
            </div>
          }
        </div>
      }

      <!-- Fri søgning -->
      <div class="card">
        <h3>Søg i tilbud</h3>
        <div class="row">
          <input class="grow" placeholder="Fx smør, kaffe, oksekød…" [(ngModel)]="query" (keyup.enter)="search()" />
          <button class="primary" (click)="search()" [disabled]="searching()">Søg</button>
        </div>
        @for (o of searchResults(); track $index) {
          <div class="list-item">
            <div class="grow">
              <div>{{ o.heading }}</div>
              @if (o.description) { <div class="muted">{{ o.description }}</div> }
            </div>
            <div style="text-align:right">
              @if (o.price != null) { <div><b>{{ o.price }} kr</b></div> }
              @if (o.store) { <div class="muted" style="font-size:.75rem">{{ o.store }}</div> }
            </div>
          </div>
        } @empty {
          @if (searchedOnce() && !searching()) { <p class="muted">Ingen tilbud fundet.</p> }
        }
      </div>
    } @else {
      <div class="empty">Tjekker tilbuds-integrationen…</div>
    }
  `
})
export class OffersPage implements OnInit {
  private api = inject(Api);
  private state = inject(WeekState);

  configured = signal<boolean | null>(null);
  matches = signal<OfferMatch[]>([]);
  matching = signal(false);
  matchedOnce = signal(false);
  searchResults = signal<Offer[]>([]);
  searching = signal(false);
  searchedOnce = signal(false);
  query = '';

  hasWeek = () => this.state.selectedWeekId() != null;

  ngOnInit() {
    this.api.getOffersStatus().subscribe({
      next: s => this.configured.set(s.configured),
      error: () => this.configured.set(false)
    });
  }

  match() {
    const id = this.state.selectedWeekId();
    if (!id) return;
    this.matching.set(true);
    this.api.matchOffers(id).subscribe({
      next: m => { this.matching.set(false); this.matchedOnce.set(true); this.matches.set(m); },
      error: () => { this.matching.set(false); this.matchedOnce.set(true); this.matches.set([]); }
    });
  }

  search() {
    if (!this.query.trim()) return;
    this.searching.set(true);
    this.api.searchOffers(this.query.trim()).subscribe({
      next: r => { this.searching.set(false); this.searchedOnce.set(true); this.searchResults.set(r); },
      error: () => { this.searching.set(false); this.searchedOnce.set(true); this.searchResults.set([]); }
    });
  }
}
