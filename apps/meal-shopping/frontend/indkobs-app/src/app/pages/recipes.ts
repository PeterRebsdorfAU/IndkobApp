import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { WeekState } from '../shared/week-state';
import { Recipe, RecipeUpsert, Ingredient, IngredientLineInput, CatalogRecipe, unitLabel } from '../models';
import { IngredientLinesEditor } from '../shared/ingredient-lines';

@Component({
  selector: 'page-recipes',
  imports: [FormsModule, IngredientLinesEditor],
  template: `
    <h1>Retter</h1>

    <!-- Faner: egne retter vs. inspiration fra kataloget -->
    <div class="row" style="margin-bottom:.8rem">
      <button class="grow" [class.primary]="tab() === 'mine'" (click)="tab.set('mine')">Mine retter</button>
      <button class="grow" [class.primary]="tab() === 'inspiration'" (click)="tab.set('inspiration')">✨ Inspiration</button>
    </div>

    @if (tab() === 'mine') {
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
          <div class="empty">Ingen retter endnu. Opret din første — eller find én under ✨ Inspiration!</div>
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
    }

    @if (tab() === 'inspiration') {
      <p class="muted">Bladr i opskrifter og tilføj dem med ét tryk — de kopieres til dine egne retter
        @if (weekLabel()) { <span>og lægges på <b>{{ weekLabel() }}</b></span> }.</p>

      <div class="field">
        <input placeholder="🔍 Søg titel, tag eller ingrediens…" [ngModel]="query()" (ngModelChange)="query.set($event)" />
      </div>

      @if (added()) { <div class="card" style="border-color:var(--primary)">✅ {{ added() }}</div> }

      @for (c of filteredCatalog(); track c.id) {
        <div class="card">
          <div class="spread">
            <div class="grow">
              <h3>{{ c.title }}</h3>
              <div class="muted">{{ c.servings }} pers.
                @for (t of c.tags; track t) { · <span>{{ t }}</span> }
              </div>
            </div>
            <button class="primary small" (click)="adopt(c)" [disabled]="adopting() === c.id">
              {{ adopting() === c.id ? '…' : '+ Tilføj' }}
            </button>
          </div>
          @if (c.note) { <div class="muted" style="margin-top:.4rem">{{ c.note }}</div> }
          <div class="row-wrap" style="margin-top:.5rem">
            @for (i of c.ingredients; track i.name) {
              <span class="pill">{{ i.name }} {{ i.quantity }} {{ label(i.unit) }}</span>
            }
          </div>
        </div>
      } @empty {
        <div class="empty">Ingen opskrifter matcher søgningen.</div>
      }
    }
  `
})
export class RecipesPage implements OnInit {
  private api = inject(Api);
  private weekState = inject(WeekState);

  recipes = signal<Recipe[]>([]);
  ingredients = signal<Ingredient[]>([]);
  editing = signal<(RecipeUpsert & { id: number | null }) | null>(null);
  error = signal('');
  saving = signal(false);

  // Inspiration
  tab = signal<'mine' | 'inspiration'>('mine');
  catalog = signal<CatalogRecipe[]>([]);
  query = signal('');
  adopting = signal<number | null>(null);
  added = signal('');
  weekLabel = signal(''); // "uge 28, 2026" hvis en uge er valgt

  filteredCatalog = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) return this.catalog();
    return this.catalog().filter(c =>
      c.title.toLowerCase().includes(q) ||
      c.tags.some(t => t.toLowerCase().includes(q)) ||
      c.ingredients.some(i => i.name.toLowerCase().includes(q)));
  });

  label = unitLabel;

  ngOnInit() {
    this.load();
    this.loadIngredients();
    this.api.getCatalog().subscribe(c => this.catalog.set(c));
    // Vis hvilken uge "Tilføj" lægger retten på (den senest valgte uge).
    const wid = this.weekState.selectedWeekId();
    if (wid) this.api.getWeek(wid).subscribe(w => this.weekLabel.set(`uge ${w.weekNumber}, ${w.year}`));
  }

  load() { this.api.getRecipes().subscribe(r => this.recipes.set(r)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  adopt(c: CatalogRecipe) {
    this.adopting.set(c.id);
    this.added.set('');
    const weekId = this.weekState.selectedWeekId();
    this.api.adoptCatalogRecipe(c.id, { weekId }).subscribe({
      next: res => {
        this.adopting.set(null);
        this.added.set(res.weekId
          ? `"${res.recipeName}" er tilføjet til dine retter og lagt på ${this.weekLabel() || 'ugen'} — ingredienserne er på indkøbslisten.`
          : `"${res.recipeName}" er tilføjet til dine retter. Læg den på en uge for at få den på indkøbslisten.`);
        this.load();
        this.loadIngredients();
      },
      error: () => { this.adopting.set(null); this.error.set('Kunne ikke tilføje.'); }
    });
  }

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
