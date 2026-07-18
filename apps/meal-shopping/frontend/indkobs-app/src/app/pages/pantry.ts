import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { PantryItem, Ingredient, Unit, UNITS, unitLabel } from '../models';

@Component({
  selector: 'page-pantry',
  imports: [FormsModule],
  template: `
    <div class="hero">
      <span class="eyebrow">Køkkenlager</span>
      <div class="hero-title">Hvad har I hjemme?</div>
      <div class="hero-sub">Indkøbslisten trækker lageret fra, så I kun køber det I mangler.</div>
    </div>

    <!-- Tilføj vare -->
    <div class="card">
      <label>Tilføj til lageret</label>
      <div class="line-input">
        <input class="full" list="pantry-ing" placeholder="Vare (fx Smør)" [(ngModel)]="newName" />
        <input type="number" min="0" step="0.001" placeholder="Antal" [(ngModel)]="newQty" />
        <select [(ngModel)]="newUnit">
          @for (u of units; track u.value) { <option [value]="u.value">{{ u.label }}</option> }
        </select>
        <button class="primary" (click)="add()" [disabled]="saving()">+</button>
      </div>
      <datalist id="pantry-ing">
        @for (i of ingredients(); track i.id) { <option [value]="i.name"></option> }
      </datalist>
      @if (error()) { <div class="error">{{ error() }}</div> }
      <p class="muted">Samme vare med forenelig enhed lægges sammen (200 g + 100 g smør = 300 g).</p>
    </div>

    <!-- Lager grupperet pr. kategori (butiksrækkefølge, ligesom indkøbslisten) -->
    @for (g of grouped(); track g.name) {
      <div class="card">
        <h3>{{ g.name }}</h3>
        @for (p of g.items; track p.id) {
          <div class="list-item">
            <span class="grow">{{ p.ingredientName }}</span>
            <input type="number" min="0" step="0.001" style="width:80px"
                   [ngModel]="p.quantity" (change)="setQty(p, $event)" />
            <select style="width:90px" [ngModel]="p.unit" (ngModelChange)="setUnit(p, $event)">
              @for (u of units; track u.value) { <option [value]="u.value">{{ u.label }}</option> }
            </select>
            <button class="danger" (click)="remove(p)" title="Fjern">✕</button>
          </div>
        }
      </div>
    } @empty {
      <div class="empty">Lageret er tomt. Tilføj det I har hjemme, så bliver indkøbslisten klogere.</div>
    }
  `
})
export class PantryPage implements OnInit {
  private api = inject(Api);

  items = signal<PantryItem[]>([]);
  ingredients = signal<Ingredient[]>([]);
  error = signal('');
  saving = signal(false);

  units = UNITS;
  label = unitLabel;

  newName = '';
  newQty = 1;
  newUnit: Unit = 'Stk';

  grouped = computed(() => {
    const groups = new Map<string, PantryItem[]>();
    for (const p of this.items()) {
      const key = p.categoryName ?? 'Andet';
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(p);
    }
    // items() kommer allerede kategori-sorteret fra API'et
    return [...groups.entries()].map(([name, items]) => ({ name, items }));
  });

  ngOnInit() {
    this.load();
    this.api.getIngredients().subscribe(i => this.ingredients.set(i));
  }

  load() { this.api.getPantry().subscribe(p => this.items.set(p)); }

  add() {
    if (!this.newName.trim()) return;
    this.error.set('');
    this.saving.set(true);
    this.api.addPantryItem({ ingredientName: this.newName.trim(), quantity: Number(this.newQty) || 1, unit: this.newUnit })
      .subscribe({
        next: () => { this.saving.set(false); this.newName = ''; this.newQty = 1; this.load(); },
        error: () => { this.saving.set(false); this.error.set('Kunne ikke tilføje.'); }
      });
  }

  setQty(p: PantryItem, ev: Event) {
    const qty = Number((ev.target as HTMLInputElement).value);
    // 0 eller mindre = brugt op → backend fjerner linjen.
    this.api.updatePantryItem(p.id, { quantity: qty, unit: p.unit }).subscribe(() => this.load());
  }

  setUnit(p: PantryItem, unit: Unit) {
    this.api.updatePantryItem(p.id, { quantity: p.quantity, unit }).subscribe(() => this.load());
  }

  remove(p: PantryItem) {
    this.api.deletePantryItem(p.id).subscribe(() => this.load());
  }
}
