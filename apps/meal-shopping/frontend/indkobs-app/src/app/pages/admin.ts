import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Api } from '../api';
import { Category, Ingredient } from '../models';

@Component({
  selector: 'page-admin',
  imports: [FormsModule],
  template: `
    <div class="hero">
      <span class="eyebrow">Varer</span>
      <div class="hero-title">Varer &amp; kategorier</div>
      <div class="hero-sub">Kategoriernes rækkefølge = indkøbslistens butiksrækkefølge.</div>
    </div>

    <!-- Kategorier -->
    <div class="card">
      <h2>Kategorier</h2>
      <p class="muted">Flyt med pilene: øverst = først på indkøbslisten (butiksrækkefølge).</p>
      @for (c of categories(); track c.id; let first = $first, last = $last) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="c.name" (change)="saveCategory(c)" />
          <button class="icon" (click)="moveCategory(c, -1)" [disabled]="first" title="Flyt op">↑</button>
          <button class="icon" (click)="moveCategory(c, 1)" [disabled]="last" title="Flyt ned">↓</button>
          <button class="danger" (click)="deleteCategory(c)">✕</button>
        </div>
      }
      <div class="row" style="margin-top:.6rem">
        <input class="grow" placeholder="Ny kategori" [(ngModel)]="newCatName" (keyup.enter)="addCategory()" />
        <button class="primary" (click)="addCategory()">+</button>
      </div>
    </div>

    <!-- Ingredienser -->
    <div class="card">
      <h2>Ingredienser</h2>
      <div class="field">
        <label>Vis kun kategori</label>
        <select [ngModel]="filter()" (ngModelChange)="filter.set($event)">
          <option [ngValue]="'alle'">Alle ({{ ingredients().length }})</option>
          @for (c of categories(); track c.id) {
            <option [ngValue]="c.id">{{ c.name }} ({{ countFor(c.id) }})</option>
          }
          <option [ngValue]="'ingen'">Uden kategori ({{ countFor(null) }})</option>
        </select>
      </div>

      @if (error()) { <div class="error">{{ error() }}</div> }
      @for (i of filteredIngredients(); track i.id) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="i.name" />
          <select style="width:140px" [(ngModel)]="i.categoryId">
            <option [ngValue]="null">(ingen)</option>
            @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
          <button class="small" (click)="saveIngredient(i)">Gem</button>
          <button class="danger" (click)="deleteIngredient(i)">✕</button>
        </div>
      } @empty { <div class="muted">Ingen ingredienser i denne kategori.</div> }

      <div class="row" style="margin-top:.6rem">
        <input class="grow" placeholder="Ny ingrediens" [(ngModel)]="newIngName" />
        <select style="width:140px" [(ngModel)]="newIngCat">
          <option [ngValue]="null">(ingen)</option>
          @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
        </select>
        <button class="primary" (click)="addIngredient()">+</button>
      </div>
    </div>
  `
})
export class AdminPage implements OnInit {
  private api = inject(Api);
  categories = signal<Category[]>([]);
  ingredients = signal<Ingredient[]>([]);
  error = signal('');

  // Kategorifilter: 'alle' | 'ingen' (uden kategori) | kategori-id
  filter = signal<'alle' | 'ingen' | number>('alle');
  filteredIngredients = computed(() => {
    const f = this.filter();
    if (f === 'alle') return this.ingredients();
    if (f === 'ingen') return this.ingredients().filter(i => i.categoryId == null);
    return this.ingredients().filter(i => i.categoryId === f);
  });

  newCatName = '';
  newIngName = '';
  newIngCat: number | null = null;

  ngOnInit() { this.loadCategories(); this.loadIngredients(); }

  loadCategories() { this.api.getCategories().subscribe(c => this.categories.set(c)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  countFor(categoryId: number | null): number {
    return this.ingredients().filter(i => i.categoryId === categoryId).length;
  }

  addCategory() {
    if (!this.newCatName.trim()) return;
    // Nye kategorier lægges nederst (højeste rækkefølge + 10).
    const nextSort = Math.max(0, ...this.categories().map(c => c.sortOrder)) + 10;
    this.api.createCategory({ name: this.newCatName.trim(), sortOrder: nextSort })
      .subscribe(() => { this.newCatName = ''; this.loadCategories(); });
  }

  saveCategory(c: Category) {
    this.api.updateCategory(c.id, { name: c.name.trim(), sortOrder: c.sortOrder }).subscribe();
  }

  // Flyt kategori op/ned: ombyt i listen og gen-nummerér rækkefølgen (10, 20, 30 …).
  moveCategory(c: Category, dir: number) {
    const cats = [...this.categories()];
    const i = cats.findIndex(x => x.id === c.id);
    const j = i + dir;
    if (j < 0 || j >= cats.length) return;
    [cats[i], cats[j]] = [cats[j], cats[i]];
    const calls = cats.map((cat, idx) =>
      this.api.updateCategory(cat.id, { name: cat.name.trim(), sortOrder: (idx + 1) * 10 }));
    forkJoin(calls).subscribe(() => this.loadCategories());
  }

  deleteCategory(c: Category) {
    if (!confirm(`Slet kategori "${c.name}"? Ingredienser beholdes uden kategori.`)) return;
    this.api.deleteCategory(c.id).subscribe(() => {
      if (this.filter() === c.id) this.filter.set('alle');
      this.loadCategories();
      this.loadIngredients();
    });
  }

  addIngredient() {
    if (!this.newIngName.trim()) return;
    this.error.set('');
    this.api.createIngredient({ name: this.newIngName.trim(), categoryId: this.newIngCat })
      .subscribe({ next: () => { this.newIngName = ''; this.newIngCat = null; this.loadIngredients(); },
                   error: () => this.error.set('Kunne ikke oprette ingrediens.') });
  }
  saveIngredient(i: Ingredient) {
    this.error.set('');
    this.api.updateIngredient(i.id, { name: i.name.trim(), categoryId: i.categoryId })
      .subscribe({ next: () => this.loadIngredients(),
                   error: () => this.error.set(`Kunne ikke gemme "${i.name}" (måske dublet-navn?).`) });
  }
  deleteIngredient(i: Ingredient) {
    if (!confirm(`Slet ingrediens "${i.name}"?`)) return;
    this.error.set('');
    this.api.deleteIngredient(i.id)
      .subscribe({ next: () => this.loadIngredients(),
                   error: () => this.error.set(`"${i.name}" er i brug (ret/varegruppe/lager) og kan ikke slettes.`) });
  }
}
