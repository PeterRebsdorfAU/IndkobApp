import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { Recipe, RecipeUpsert, Ingredient, IngredientLineInput, unitLabel } from '../models';
import { IngredientLinesEditor } from '../shared/ingredient-lines';

@Component({
  selector: 'page-recipes',
  imports: [FormsModule, IngredientLinesEditor],
  template: `
    <h1>Retter</h1>
    <p class="muted">Dine opskrifter. Vælg dem til en uge på Uge-fanen.</p>

    @if (!editing()) {
      <button class="primary" (click)="startNew()">+ Ny ret</button>

      @for (r of recipes(); track r.id) {
        <div class="card">
          <div class="spread">
            <div class="grow">
              <h3>{{ r.name }}</h3>
              <div class="muted">{{ r.servings }} pers. · {{ r.ingredients.length }} ingredienser</div>
            </div>
            <div class="row">
              <button class="small" (click)="edit(r)">Rediger</button>
              <button class="danger" (click)="remove(r)">Slet</button>
            </div>
          </div>
          @if (r.note) { <div class="muted" style="margin-top:.4rem">{{ r.note }}</div> }
          <div class="row-wrap" style="margin-top:.5rem">
            @for (i of r.ingredients; track i.id) {
              <span class="pill">{{ i.ingredientName }} {{ i.quantity }} {{ label(i.unit) }}</span>
            }
          </div>
        </div>
      } @empty {
        <div class="empty">Ingen retter endnu. Opret din første!</div>
      }
    }

    @if (editing(); as form) {
      <div class="card">
        <h2>{{ form.id ? 'Rediger ret' : 'Ny ret' }}</h2>
        <div class="field">
          <label>Navn</label>
          <input [(ngModel)]="form.name" placeholder="Fx Spaghetti med kødsovs" />
        </div>
        <div class="field">
          <label>Antal personer (basis)</label>
          <input type="number" min="1" [(ngModel)]="form.servings" />
        </div>
        <div class="field">
          <label>Note (valgfri)</label>
          <textarea [(ngModel)]="form.note"></textarea>
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
export class RecipesPage implements OnInit {
  private api = inject(Api);
  recipes = signal<Recipe[]>([]);
  ingredients = signal<Ingredient[]>([]);
  editing = signal<(RecipeUpsert & { id: number | null }) | null>(null);
  error = signal('');
  saving = signal(false);

  ngOnInit() { this.load(); this.loadIngredients(); }

  label = unitLabel;

  load() { this.api.getRecipes().subscribe(r => this.recipes.set(r)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  startNew() {
    this.error.set('');
    this.editing.set({ id: null, name: '', note: '', servings: 4, ingredients: [] });
  }

  edit(r: Recipe) {
    this.error.set('');
    const ingredients: IngredientLineInput[] = r.ingredients.map(i => ({
      ingredientId: i.ingredientId, ingredientName: i.ingredientName, quantity: i.quantity, unit: i.unit
    }));
    this.editing.set({ id: r.id, name: r.name, note: r.note ?? '', servings: r.servings, ingredients });
  }

  cancel() { this.editing.set(null); }

  save() {
    const form = this.editing();
    if (!form) return;
    if (!form.name.trim()) { this.error.set('Navn skal udfyldes.'); return; }
    const payload: RecipeUpsert = {
      name: form.name.trim(),
      note: form.note?.trim() || null,
      servings: Number(form.servings) || 1,
      ingredients: form.ingredients.filter(l => l.ingredientName.trim())
    };
    this.saving.set(true);
    const obs = form.id
      ? this.api.updateRecipe(form.id, payload)
      : this.api.createRecipe(payload);
    obs.subscribe({
      next: () => { this.saving.set(false); this.editing.set(null); this.load(); this.loadIngredients(); },
      error: () => { this.saving.set(false); this.error.set('Kunne ikke gemme. Kører backend?'); }
    });
  }

  remove(r: Recipe) {
    if (!confirm(`Slet "${r.name}"?`)) return;
    this.api.deleteRecipe(r.id).subscribe(() => this.load());
  }
}
