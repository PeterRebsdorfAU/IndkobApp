import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Auth } from '../auth';
import { markOnboardingSeen } from '../shared/onboarding-state';

/** Ét trin i guiden. */
interface Step {
  icon: string;
  title: string;
  body: string;
}

/**
 * Første-gangs-onboarding: en kort, guidet gennemgang af flowet
 * madplan → indkøbsliste → lager → ordrer. Vises kun første gang
 * (styret via localStorage i shared/onboarding-state.ts og app.ts).
 * Kan altid ses igen fra FAQ/hjælp-siden.
 */
@Component({
  selector: 'page-onboarding',
  template: `
    <div class="ob-wrap">
      <div class="card ob-card">
        <div class="ob-top">
          <span class="brand">🛒 Madplan &amp; Indkøb</span>
          <button class="btn-link" (click)="finish()">Spring over</button>
        </div>

        @if (current(); as s) {
          <div class="ob-body">
            <div class="ob-icon" aria-hidden="true">{{ s.icon }}</div>
            <h1>{{ s.title }}</h1>
            <p>{{ s.body }}</p>
          </div>
        }

        <!-- Trin-prikker -->
        <div class="ob-dots" role="tablist" aria-label="Trin">
          @for (s of steps; track $index) {
            <span class="dot" [class.on]="$index === step()" (click)="step.set($index)"></span>
          }
        </div>

        <div class="ob-actions">
          @if (step() > 0) {
            <button (click)="prev()">Tilbage</button>
          } @else {
            <span class="grow"></span>
          }
          <span class="grow"></span>
          @if (!isLast()) {
            <button class="primary" (click)="next()">Videre</button>
          } @else {
            <button class="primary" (click)="finish()">Kom i gang</button>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .ob-wrap { min-height: 82vh; display: flex; align-items: center; justify-content: center; }
    .ob-card { width: 100%; max-width: 420px; }
    .ob-top { display: flex; align-items: center; justify-content: space-between; margin-bottom: .5rem; }
    .ob-top .brand { font-weight: 600; color: var(--primary); }
    .ob-body { text-align: center; padding: 1.2rem .5rem 1rem; }
    .ob-icon { font-size: 3rem; line-height: 1; margin-bottom: .6rem; }
    .ob-body h1 { font-size: 1.25rem; margin-bottom: .5rem; }
    .ob-body p { color: var(--muted); line-height: 1.5; margin: 0 auto; max-width: 32ch; }
    .ob-dots { display: flex; gap: .4rem; justify-content: center; margin: .4rem 0 1rem; }
    .ob-dots .dot {
      width: 8px; height: 8px; border-radius: 999px;
      background: var(--border); cursor: pointer; transition: background .2s, width .2s;
    }
    .ob-dots .dot.on { background: var(--primary); width: 20px; }
    .ob-actions { display: flex; align-items: center; gap: .5rem; }
    .ob-actions .grow { flex: 1 1 auto; }
  `]
})
export class OnboardingPage {
  private router = inject(Router);
  private auth = inject(Auth);

  // Husstandsnavn hvis vi har det (personliggør velkomsten en smule).
  private household = this.auth.householdName();

  readonly steps: Step[] = [
    {
      icon: '👋',
      title: this.household ? `Velkommen, ${this.household}!` : 'Velkommen!',
      body: 'Madplan & Indkøb hjælper jer med at planlægge ugens mad og få én samlet, ' +
            'smart indkøbsliste. Her er de fire trin i flowet.'
    },
    {
      icon: '📅',
      title: '1. Planlæg ugen',
      body: 'På Uge-fanen vælger I dagens retter og varegrupper (fx Frokost). ' +
            'I kan skalere portioner og tilføje løse varer.'
    },
    {
      icon: '🛒',
      title: '2. Få indkøbslisten',
      body: 'Appen lægger ens varer sammen, omregner enheder og sorterer efter ' +
            'butikkens rækkefølge. Kryds af mens I handler.'
    },
    {
      icon: '🥫',
      title: '3. Hold styr på lageret',
      body: 'Skriv hvad I har hjemme på Lager-fanen. Så trækker indkøbslisten det fra, ' +
            'og I køber kun det, der mangler.'
    },
    {
      icon: '📦',
      title: '4. Send til butik',
      body: 'Er din butik med? Send hele indkøbslisten som en ordre og følg status ' +
            'fra Modtaget til Afhentet. Ellers handler I bare selv efter listen.'
    }
  ];

  step = signal(0);
  current = computed(() => this.steps[this.step()]);
  isLast = computed(() => this.step() === this.steps.length - 1);

  next() { if (!this.isLast()) this.step.update(s => s + 1); }
  prev() { if (this.step() > 0) this.step.update(s => s - 1); }

  finish() {
    markOnboardingSeen();
    this.router.navigateByUrl('/uge');
  }
}
