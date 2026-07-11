import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, isoWeek } from '../api';
import { WeekState } from '../shared/week-state';
import { Week, WeekDetail, Recipe, ItemGroup, Ingredient, Unit, UNITS, DAYS, unitLabel } from '../models';

@Component({
  selector: 'page-week-plan',
  imports: [FormsModule, RouterLink],
  template: `
    <h1>Ugeplan</h1>

    <!-- Uge-vælger -->
    <div class="card">
      <div class="field">
        <label>Vælg uge</label>
        <select [ngModel]="selectedId()" (ngModelChange)="onSelect($event)">
          <option [ngValue]="null">– vælg –</option>
          @for (w of weeks(); track w.id) {
            <option [ngValue]="w.id">Uge {{ w.weekNumber }}, {{ w.year }}</option>
          }
        </select>
      </div>
      <div class="row-wrap">
        <div class="grow row">
          <div>
            <label>År</label>
            <input type="number" [(ngModel)]="newYear" style="width:90px" />
          </div>
          <div>
            <label>Uge</label>
            <input type="number" min="1" max="53" [(ngModel)]="newWeek" style="width:80px" />
          </div>
        </div>
        <button class="primary" (click)="createWeek()">+ Opret uge</button>
      </div>
    </div>

    @if (detail(); as d) {
      <div class="spread">
        <h2>Uge {{ d.weekNumber }}, {{ d.year }}</h2>
        <div class="row">
          <a routerLink="/indkob"><button class="small primary">🛒 Indkøbsliste</button></a>
          <button class="danger" (click)="deleteWeek(d.id)">Slet uge</button>
        </div>
      </div>

      <!-- Retter i ugen -->
      <div class="card">
        <h3>Retter</h3>
        @for (wr of d.recipes; track wr.id) {
          <div class="list-item">
            <div class="grow">
              <div>{{ wr.recipeName }} @if (wr.cookedUtc) { <span class="badge">✅ lavet</span> }</div>
              <div class="muted">Basis {{ wr.baseServings }} pers.</div>
              @if (!wr.cookedUtc) {
                <button class="small" style="margin-top:.25rem" (click)="markCooked(wr.id)">🍳 Lavet</button>
              } @else {
                <button class="btn-link" style="font-size:.75rem" (click)="unmarkCooked(wr.id)">fortryd</button>
              }
            </div>
            <div>
              <label>Personer</label>
              <input type="number" min="1" style="width:64px"
                     [ngModel]="wr.servings ?? wr.baseServings"
                     (ngModelChange)="setServings(wr.id, $event)" />
            </div>
            <div>
              <label>Dag</label>
              <select style="width:110px" [ngModel]="wr.dayOfWeek"
                      (ngModelChange)="setDay(wr.id, $event)">
                <option [ngValue]="null">–</option>
                @for (day of days; track $index) { <option [ngValue]="$index">{{ day }}</option> }
              </select>
            </div>
            <button class="danger" (click)="removeRecipe(wr.id)">✕</button>
          </div>
        } @empty { <div class="muted">Ingen retter tilføjet.</div> }

        <div class="row" style="margin-top:.6rem">
          <select class="grow" [(ngModel)]="addRecipeId">
            <option [ngValue]="null">Vælg ret…</option>
            @for (r of recipes(); track r.id) { <option [ngValue]="r.id">{{ r.name }}</option> }
          </select>
          <button class="primary" (click)="addRecipe()" [disabled]="!addRecipeId">Tilføj</button>
        </div>
      </div>

      <!-- Varegrupper i ugen -->
      <div class="card">
        <h3>Varegrupper</h3>
        @for (wg of d.itemGroups; track wg.id) {
          <div class="list-item">
            <div class="grow">{{ wg.itemGroupName }}</div>
            <button class="danger" (click)="removeGroup(wg.id)">✕</button>
          </div>
        } @empty { <div class="muted">Ingen varegrupper tilføjet.</div> }

        <div class="row" style="margin-top:.6rem">
          <select class="grow" [(ngModel)]="addGroupId">
            <option [ngValue]="null">Vælg varegruppe…</option>
            @for (g of groups(); track g.id) { <option [ngValue]="g.id">{{ g.name }}</option> }
          </select>
          <button class="primary" (click)="addGroup()" [disabled]="!addGroupId">Tilføj</button>
        </div>
      </div>

      <!-- Løse varer -->
      <div class="card">
        <h3>Løse varer</h3>
        @for (m of d.manualItems; track m.id) {
          <div class="list-item">
            <div class="grow">{{ m.name }} <span class="muted">{{ m.quantity }} {{ label(m.unit) }}</span></div>
            <button class="danger" (click)="removeManual(m.id)">✕</button>
          </div>
        } @empty { <div class="muted">Ingen løse varer.</div> }

        <div class="line-input" style="margin-top:.6rem">
          <input class="full" list="manual-ing" placeholder="Vare (fx Kaffe)" [(ngModel)]="manualText" />
          <input type="number" min="0" step="0.001" placeholder="Antal" [(ngModel)]="manualQty" />
          <select [(ngModel)]="manualUnit">
            @for (u of units; track u.value) { <option [value]="u.value">{{ u.label }}</option> }
          </select>
          <button class="primary" (click)="addManual()">+</button>
        </div>
        <datalist id="manual-ing">
          @for (i of ingredients(); track i.id) { <option [value]="i.name"></option> }
        </datalist>
      </div>
    } @else {
      <div class="empty">Vælg eller opret en uge for at planlægge.</div>
    }
  `
})
export class WeekPlanPage implements OnInit {
  private api = inject(Api);
  private state = inject(WeekState);

  weeks = signal<Week[]>([]);
  recipes = signal<Recipe[]>([]);
  groups = signal<ItemGroup[]>([]);
  ingredients = signal<Ingredient[]>([]);
  detail = signal<WeekDetail | null>(null);
  selectedId = this.state.selectedWeekId;

  days = DAYS;
  units = UNITS;
  label = unitLabel;

  // Formular-felter
  newYear = isoWeek(new Date()).year;
  newWeek = isoWeek(new Date()).week;
  addRecipeId: number | null = null;
  addGroupId: number | null = null;
  manualText = '';
  manualQty = 1;
  manualUnit: Unit = 'Stk';

  ngOnInit() {
    this.api.getRecipes().subscribe(r => this.recipes.set(r));
    this.api.getItemGroups().subscribe(g => this.groups.set(g));
    this.api.getIngredients().subscribe(i => this.ingredients.set(i));
    this.loadWeeks();
  }

  loadWeeks() {
    this.api.getWeeks().subscribe(w => {
      this.weeks.set(w);
      const sel = this.selectedId();
      if (sel && w.some(x => x.id === sel)) this.loadDetail(sel);
      else { this.state.select(null); this.detail.set(null); }
    });
  }

  onSelect(id: number | null) {
    this.state.select(id);
    if (id) this.loadDetail(id); else this.detail.set(null);
  }

  loadDetail(id: number) { this.api.getWeek(id).subscribe(d => this.detail.set(d)); }

  createWeek() {
    this.api.createWeek({ year: Number(this.newYear), weekNumber: Number(this.newWeek) }).subscribe(w => {
      this.state.select(w.id);
      this.loadWeeks();
      this.loadDetail(w.id);
    });
  }

  deleteWeek(id: number) {
    if (!confirm('Slet hele ugen og dens plan?')) return;
    this.api.deleteWeek(id).subscribe(() => { this.state.select(null); this.detail.set(null); this.loadWeeks(); });
  }

  private wid() { return this.detail()!.id; }

  addRecipe() {
    if (!this.addRecipeId) return;
    this.api.addWeekRecipe(this.wid(), { recipeId: this.addRecipeId }).subscribe(d => { this.detail.set(d); this.addRecipeId = null; });
  }
  setServings(weekRecipeId: number, servings: number) {
    const wr = this.detail()!.recipes.find(x => x.id === weekRecipeId)!;
    this.api.updateWeekRecipe(this.wid(), weekRecipeId, { servings: Number(servings), dayOfWeek: wr.dayOfWeek })
      .subscribe(d => this.detail.set(d));
  }
  setDay(weekRecipeId: number, dayOfWeek: number | null) {
    const wr = this.detail()!.recipes.find(x => x.id === weekRecipeId)!;
    this.api.updateWeekRecipe(this.wid(), weekRecipeId, { servings: wr.servings, dayOfWeek })
      .subscribe(d => this.detail.set(d));
  }
  removeRecipe(weekRecipeId: number) {
    this.api.removeWeekRecipe(this.wid(), weekRecipeId).subscribe(d => this.detail.set(d));
  }

  // "Lavet": trækker rettens ingredienser (skaleret) fra køkkenlageret.
  markCooked(weekRecipeId: number) {
    if (!confirm('Markér som lavet? Ingredienserne trækkes fra køkkenlageret.')) return;
    this.api.markCooked(this.wid(), weekRecipeId).subscribe(d => this.detail.set(d));
  }
  unmarkCooked(weekRecipeId: number) {
    if (!confirm('Fortryd "lavet"-markeringen? (Lageret føres ikke tilbage automatisk.)')) return;
    this.api.unmarkCooked(this.wid(), weekRecipeId).subscribe(d => this.detail.set(d));
  }

  addGroup() {
    if (!this.addGroupId) return;
    this.api.addWeekItemGroup(this.wid(), this.addGroupId).subscribe(d => { this.detail.set(d); this.addGroupId = null; });
  }
  removeGroup(weekItemGroupId: number) {
    this.api.removeWeekItemGroup(this.wid(), weekItemGroupId).subscribe(d => this.detail.set(d));
  }

  addManual() {
    if (!this.manualText.trim()) return;
    this.api.addWeekManualItem(this.wid(), { freeText: this.manualText.trim(), quantity: Number(this.manualQty) || 1, unit: this.manualUnit })
      .subscribe(d => { this.detail.set(d); this.manualText = ''; this.manualQty = 1; });
  }
  removeManual(manualItemId: number) {
    this.api.removeWeekManualItem(this.wid(), manualItemId).subscribe(d => this.detail.set(d));
  }
}
