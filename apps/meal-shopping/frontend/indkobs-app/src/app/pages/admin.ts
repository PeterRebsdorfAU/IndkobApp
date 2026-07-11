import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { Category, Ingredient } from '../models';

@Component({
  selector: 'page-admin',
  imports: [FormsModule],
  template: `
    <h1>Administration</h1>
    <p class="muted">Ingredienser og kategorier. Kategori bestemmer butiksrækkefølgen på indkøbslisten.</p>

    <!-- Kategorier -->
    <div class="card">
      <h2>Kategorier</h2>
      @for (c of categories(); track c.id) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="c.name" />
          <input type="number" style="width:70px" [(ngModel)]="c.sortOrder" title="Rækkefølge" />
          <button class="small" (click)="saveCategory(c)">Gem</button>
          <button class="danger" (click)="deleteCategory(c)">✕</button>
        </div>
      }
      <div class="row" style="margin-top:.6rem">
        <input class="grow" placeholder="Ny kategori" [(ngModel)]="newCatName" />
        <input type="number" style="width:70px" placeholder="Sort" [(ngModel)]="newCatSort" />
        <button class="primary" (click)="addCategory()">+</button>
      </div>
    </div>

    <!-- Ingredienser -->
    <div class="card">
      <h2>Ingredienser</h2>
      @if (error()) { <div class="error">{{ error() }}</div> }
      @for (i of ingredients(); track i.id) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="i.name" />
          <select style="width:140px" [(ngModel)]="i.categoryId">
            <option [ngValue]="null">(ingen)</option>
            @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
          <button class="small" (click)="saveIngredient(i)">Gem</button>
          <button class="danger" (click)="deleteIngredient(i)">✕</button>
        </div>
      } @empty { <div class="muted">Ingen ingredienser endnu.</div> }

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

  newCatName = '';
  newCatSort = 100;
  newIngName = '';
  newIngCat: number | null = null;

  ngOnInit() { this.loadCategories(); this.loadIngredients(); }

  loadCategories() { this.api.getCategories().subscribe(c => this.categories.set(c)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  addCategory() {
    if (!this.newCatName.trim()) return;
    this.api.createCategory({ name: this.newCatName.trim(), sortOrder: Number(this.newCatSort) || 100 })
      .subscribe(() => { this.newCatName = ''; this.loadCategories(); });
  }
  saveCategory(c: Category) {
    this.api.updateCategory(c.id, { name: c.name.trim(), sortOrder: Number(c.sortOrder) || 0 }).subscribe();
  }
  deleteCategory(c: Category) {
    if (!confirm(`Slet kategori "${c.name}"? Ingredienser beholdes uden kategori.`)) return;
    this.api.deleteCategory(c.id).subscribe(() => { this.loadCategories(); this.loadIngredients(); });
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
                   error: () => this.error.set(`"${i.name}" bruges i en ret/varegruppe og kan ikke slettes.`) });
  }
}
