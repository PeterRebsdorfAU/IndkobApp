import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { ItemGroup, ItemGroupUpsert, Ingredient, IngredientLineInput, unitLabel } from '../models';
import { IngredientLinesEditor } from '../shared/ingredient-lines';

@Component({
  selector: 'page-item-groups',
  imports: [FormsModule, IngredientLinesEditor],
  template: `
    <div class="hero">
      <span class="eyebrow">Varegrupper</span>
      <div class="hero-title">Faste vare-sæt</div>
      <div class="hero-sub">Sæt af varer der ikke er retter (fx Frokost, Toilet, Rengøring).</div>
    </div>

    @if (!editing()) {
      <button class="primary" (click)="startNew()">+ Ny varegruppe</button>

      @for (g of groups(); track g.id) {
        <div class="card">
          <div class="spread">
            <div class="grow">
              <h3>{{ g.name }}</h3>
              <div class="muted">{{ g.ingredients.length }} varer</div>
            </div>
            <div class="row">
              <button class="small" (click)="edit(g)">Rediger</button>
              <button class="danger" (click)="remove(g)">Slet</button>
            </div>
          </div>
          <div class="row-wrap" style="margin-top:.5rem">
            @for (i of g.ingredients; track i.id) {
              <span class="pill">{{ i.ingredientName }} {{ i.quantity }} {{ label(i.unit) }}</span>
            }
          </div>
        </div>
      } @empty {
        <div class="empty">Ingen varegrupper endnu.</div>
      }
    }

    @if (editing(); as form) {
      <div class="card">
        <h2>{{ form.id ? 'Rediger varegruppe' : 'Ny varegruppe' }}</h2>
        <div class="field">
          <label>Navn</label>
          <input [(ngModel)]="form.name" placeholder="Fx Frokost" />
        </div>

        <ingredient-lines [(lines)]="form.ingredients" [ingredients]="ingredients()" />

        @if (error()) { <div class="error">{{ error() }}</div> }
        <div class="row" style="margin-top:.8rem">
          <button class="primary grow" (click)="save()" [disabled]="saving()">Gem</button>
          <button class="grow" (click)="cancel()">Annullér</button>
        </div>
      </div>
    }
  `
})
export class ItemGroupsPage implements OnInit {
  private api = inject(Api);
  groups = signal<ItemGroup[]>([]);
  ingredients = signal<Ingredient[]>([]);
  editing = signal<(ItemGroupUpsert & { id: number | null }) | null>(null);
  error = signal('');
  saving = signal(false);

  label = unitLabel;

  ngOnInit() { this.load(); this.loadIngredients(); }

  load() { this.api.getItemGroups().subscribe(g => this.groups.set(g)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  startNew() { this.error.set(''); this.editing.set({ id: null, name: '', ingredients: [] }); }

  edit(g: ItemGroup) {
    this.error.set('');
    const ingredients: IngredientLineInput[] = g.ingredients.map(i => ({
      ingredientId: i.ingredientId, ingredientName: i.ingredientName, quantity: i.quantity, unit: i.unit
    }));
    this.editing.set({ id: g.id, name: g.name, ingredients });
  }

  cancel() { this.editing.set(null); }

  save() {
    const form = this.editing();
    if (!form) return;
    if (!form.name.trim()) { this.error.set('Navn skal udfyldes.'); return; }
    const payload: ItemGroupUpsert = {
      name: form.name.trim(),
      ingredients: form.ingredients.filter(l => l.ingredientName.trim())
    };
    this.saving.set(true);
    const obs = form.id ? this.api.updateItemGroup(form.id, payload) : this.api.createItemGroup(payload);
    obs.subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); this.loadIngredients(); },
      error: () => { this.saving.set(false); this.error.set('Kunne ikke gemme.'); }
    });
  }

  remove(g: ItemGroup) {
    if (!confirm(`Slet "${g.name}"?`)) return;
    this.api.deleteItemGroup(g.id).subscribe(() => this.load());
  }
}
