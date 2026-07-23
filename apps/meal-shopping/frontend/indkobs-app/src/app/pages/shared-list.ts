import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Api } from '../api';
import { ShoppingList, ShoppingLine, unitLabel } from '../models';
import { LogoMark } from '../shared/logo';

/**
 * Offentlig del-side: viser en DELT indkøbsliste via token i URL'en (/del/:token).
 * Ingen login — modtageren kan se listen og krydse af, intet andet.
 */
@Component({
  selector: 'page-shared-list',
  imports: [LogoMark],
  template: `
    <div class="hero">
      <span class="eyebrow" style="align-items:center">
        <app-logo [size]="20" /> Madplan
      </span>
      <div class="hero-title" style="font-size:1.8rem">Indkøbsliste</div>
      @if (list(); as l) {
        <div class="hero-sub">Uge {{ l.weekNumber }}, {{ l.year }} · delt med dig · {{ checkedCount() }}/{{ totalCount() }} købt</div>
      }
    </div>

    @if (list(); as l) {

      @for (g of l.groups; track g.categoryName) {
        <div class="card">
          <h3>{{ g.categoryName }}</h3>
          @for (line of g.lines; track line.lineKey) {
            <label class="list-item" style="cursor:pointer">
              <input type="checkbox" class="check" [checked]="line.isChecked"
                     (change)="toggle(line)" />
              <span class="grow" [class.checked]="line.isChecked || line.quantity === 0">
                {{ line.name }} — {{ qty(line.quantity) }} {{ label(line.unit) }}
                @if (line.quantity === 0) { <span class="badge">haves allerede</span> }
              </span>
            </label>
          }
        </div>
      }
    } @else if (failed()) {
      <div class="empty">Linket er udløbet eller delingen er stoppet.<br />Bed om et nyt link.</div>
    } @else {
      <div class="empty">Henter listen… (kan tage op til et minut første gang)</div>
    }
  `,
  styles: [`
    :host .hero .eyebrow app-logo { line-height: 0; }
  `]
})
export class SharedListPage implements OnInit {
  private api = inject(Api);
  private route = inject(ActivatedRoute);

  list = signal<ShoppingList | null>(null);
  failed = signal(false);
  private token = '';

  totalCount = computed(() => this.list()?.groups.reduce((s, g) => s + g.lines.length, 0) ?? 0);
  checkedCount = computed(() =>
    this.list()?.groups.reduce((s, g) => s + g.lines.filter(l => l.isChecked).length, 0) ?? 0);

  label = unitLabel;

  ngOnInit() {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.api.getSharedList(this.token).subscribe({
      next: l => this.list.set(l),
      error: () => this.failed.set(true)
    });
  }

  toggle(line: ShoppingLine) {
    const next = !line.isChecked;
    line.isChecked = next;
    this.list.update(l => l ? { ...l } : l);
    this.api.setSharedCheck(this.token, line.lineKey, next)
      .subscribe({ error: () => { line.isChecked = !next; } });
  }

  qty(n: number): string {
    return n.toLocaleString('da-DK', { maximumFractionDigits: 3 });
  }
}
