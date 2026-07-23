import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, isoWeek } from '../api';
import { WeekState } from '../shared/week-state';
import { EmptyState } from '../shared/empty-state';
import { Week, WeekDetail, WeekRecipe, Recipe, ItemGroup, Ingredient, Unit, UNITS, DAYS, unitLabel } from '../models';

@Component({
  selector: 'page-week-plan',
  imports: [FormsModule, RouterLink, EmptyState],
  template: `
    @if (detail(); as d) {
      <div class="hero">
        <span class="eyebrow">Ugeplan</span>
        <div class="hero-title">Uge {{ d.weekNumber }}</div>
        <div class="hero-sub">{{ d.year }} · {{ d.recipes.length }} retter · {{ d.itemGroups.length }} varegrupper</div>
        <div class="hero-actions">
          <a routerLink="/indkob"><button class="accent small">Se indkøbsliste →</button></a>
          <button class="ghost small" style="color:#fff" (click)="deleteWeek(d.id)">Slet uge</button>
        </div>
      </div>
    } @else {
      <div class="hero">
        <span class="eyebrow">Ugeplan</span>
        <div class="hero-title">Planlæg din uge</div>
        <div class="hero-sub">Vælg ugens retter og få én samlet indkøbsliste.</div>
      </div>
    }

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
      <!-- Retter i ugen: vælg DAG først, så ret -->
      <div class="card">
        <h3>Retter</h3>
        <p class="muted">Tryk "+ Ret" på en dag og vælg retten.</p>

        @for (g of dayGroups(); track g.label) {
          <div class="day-group">
            <div class="spread" style="padding:.35rem 0">
              <b [class.muted]="g.recipes.length === 0">{{ g.label }}</b>
              <button class="small" (click)="openAdd(g.value)">+ Ret</button>
            </div>

            @for (wr of g.recipes; track wr.id) {
              <div class="list-item">
                <div class="grow">
                  <div><b>{{ wr.recipeName }}</b> @if (wr.cookedUtc) { <span class="badge ready">Lavet</span> }</div>
                  <div class="muted">Basis {{ wr.baseServings }} pers.</div>
                  @if (!wr.cookedUtc) {
                    <button class="small" style="margin-top:.35rem" (click)="markCooked(wr.id)">Markér som lavet</button>
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
                <button class="danger" (click)="removeRecipe(wr.id)">✕</button>
              </div>
            }

            @if (addDay() === g.value) {
              <div class="row" style="margin:.4rem 0 .6rem">
                <select class="grow" [(ngModel)]="addRecipeId">
                  <option [ngValue]="null">Vælg ret til {{ g.label.toLowerCase() }}…</option>
                  @for (r of recipes(); track r.id) { <option [ngValue]="r.id">{{ r.name }}</option> }
                </select>
                <button class="primary" (click)="addRecipe()" [disabled]="!addRecipeId">Tilføj</button>
                <button (click)="closeAdd()">✕</button>
              </div>
            }
          </div>
        }
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
      <app-empty-state icon="📅" title="Planlæg din første uge"
        text="Vælg en uge ovenfor — eller opret en ny — og tilføj retter og varegrupper til dagene." />
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

  // Hvilken dag ret-vælgeren er åben for (undefined = lukket; null = "Uden bestemt dag").
  addDay = signal<number | null | undefined>(undefined);

  // Ugens retter grupperet pr. dag (dag-først-flowet). "Uden bestemt dag" til sidst.
  dayGroups = computed(() => {
    const d = this.detail();
    if (!d) return [];
    const groups: { value: number | null; label: string; recipes: WeekRecipe[] }[] =
      this.days.map((label, i) => ({ value: i, label, recipes: d.recipes.filter(r => r.dayOfWeek === i) }));
    groups.push({ value: null, label: 'Uden bestemt dag', recipes: d.recipes.filter(r => r.dayOfWeek == null) });
    return groups;
  });

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

  // Dag-først-flow: åbn ret-vælgeren under en bestemt dag (null = "Uden bestemt dag").
  openAdd(day: number | null) {
    this.addRecipeId = null;
    this.addDay.set(day);
  }
  closeAdd() { this.addDay.set(undefined); }

  addRecipe() {
    if (!this.addRecipeId || this.addDay() === undefined) return;
    this.api.addWeekRecipe(this.wid(), { recipeId: this.addRecipeId, dayOfWeek: this.addDay() })
      .subscribe(d => { this.detail.set(d); this.addRecipeId = null; this.closeAdd(); });
  }
  setServings(weekRecipeId: number, servings: number) {
    const wr = this.detail()!.recipes.find(x => x.id === weekRecipeId)!;
    this.api.updateWeekRecipe(this.wid(), weekRecipeId, { servings: Number(servings), dayOfWeek: wr.dayOfWeek })
      .subscribe(d => this.detail.set(d));
  }
  removeRecipe(weekRecipeId: number) {
    this.api.removeWeekRecipe(this.wid(), weekRecipeId).subscribe(d => this.detail.set(d));
  }

  // "Lavet": markerer retten som lavet i ugen (historik).
  markCooked(weekRecipeId: number) {
    this.api.markCooked(this.wid(), weekRecipeId).subscribe(d => this.detail.set(d));
  }
  unmarkCooked(weekRecipeId: number) {
    if (!confirm('Fortryd "lavet"-markeringen?')) return;
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
