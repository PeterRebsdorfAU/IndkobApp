import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { forkJoin, of, Subject } from 'rxjs';
import { catchError, debounceTime, groupBy, mergeMap, switchMap, tap } from 'rxjs/operators';
import { Api } from '../api';
import { ToastService } from '../shared/toast';
import { Category, Ingredient } from '../models';

// Status pr. række i auto-gem-flowet.
type SaveStatus = 'saving' | 'saved' | 'error';

@Component({
  selector: 'page-admin',
  imports: [FormsModule],
  template: `
    <div class="hero">
      <span class="eyebrow">Varer</span>
      <div class="hero-title">Varer &amp; kategorier</div>
      <div class="hero-sub">Ændringer gemmes automatisk. Kategoriernes rækkefølge = indkøbslistens butiksrækkefølge.</div>
    </div>

    <!-- Kategorier -->
    <div class="card">
      <h2>Kategorier</h2>
      <p class="muted">Flyt med pilene: øverst = først på indkøbslisten (butiksrækkefølge).</p>
      @for (c of categories(); track c.id; let first = $first, last = $last) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="c.name" (ngModelChange)="queueCategory(c)" />
          @if (status('c', c.id); as st) {
            @if (st === 'error') {
              <button class="primary small unsaved" (click)="saveCategoryNow(c)" title="Gem ændringer nu">Gem ændringer •</button>
            } @else {
              <span class="save-ind" [class.ok]="st === 'saved'">{{ st === 'saving' ? 'Gemmer…' : 'Gemt ✓' }}</span>
            }
          }
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

      @for (i of filteredIngredients(); track i.id) {
        <div class="list-item">
          <input class="grow" [(ngModel)]="i.name" (ngModelChange)="queueIngredient(i)" />
          <select style="width:140px" [(ngModel)]="i.categoryId" (ngModelChange)="queueIngredient(i)">
            <option [ngValue]="null">(ingen)</option>
            @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
          @if (status('i', i.id); as st) {
            @if (st === 'error') {
              <button class="primary small unsaved" (click)="saveIngredientNow(i)" title="Gem ændringer nu">Gem •</button>
            } @else {
              <span class="save-ind" [class.ok]="st === 'saved'">{{ st === 'saving' ? 'Gemmer…' : 'Gemt ✓' }}</span>
            }
          }
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
  `,
  styles: [`
    /* Diskret auto-gem-indikator ved hver række */
    .save-ind {
      flex: 0 0 auto; font-size: .78rem; color: var(--muted);
      white-space: nowrap; font-variant-numeric: tabular-nums;
    }
    .save-ind.ok { color: var(--forest); font-weight: 700; }
    /* Fremhævet fald-tilbage-knap: vises kun når et auto-gem fejlede (ugemte ændringer). */
    button.small.unsaved {
      flex: 0 0 auto; min-height: 34px; padding: .3rem .7rem; font-size: .8rem;
      animation: unsaved-pulse 1.5s ease-in-out infinite;
    }
    @keyframes unsaved-pulse {
      0%, 100% { box-shadow: 0 0 0 0 rgba(216, 69, 58, .45); }
      50%      { box-shadow: 0 0 0 5px rgba(216, 69, 58, 0); }
    }
    @media (prefers-reduced-motion: reduce) {
      button.small.unsaved { animation: none; }
    }
  `]
})
export class AdminPage implements OnInit {
  private api = inject(Api);
  private toast = inject(ToastService);

  categories = signal<Category[]>([]);
  ingredients = signal<Ingredient[]>([]);

  // Per-række gem-status ('c'<id> for kategorier, 'i'<id> for ingredienser).
  private saveStatus = signal<Record<string, SaveStatus>>({});

  // Debounce-tid for auto-gem (ms) efter sidste tastetryk/ændring.
  private readonly DEBOUNCE = 700;
  private catSave$ = new Subject<Category>();
  private ingSave$ = new Subject<Ingredient>();

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

  constructor() {
    // Per-entitet debounce: grupper på id, så to forskellige rækker ikke afbryder
    // hinanden, men gentagne tastetryk i SAMME række samles til ét gem.
    this.catSave$.pipe(
      groupBy(c => c.id),
      mergeMap(g => g.pipe(debounceTime(this.DEBOUNCE), switchMap(c => this.doSaveCategory(c)))),
      takeUntilDestroyed(),
    ).subscribe();

    this.ingSave$.pipe(
      groupBy(i => i.id),
      mergeMap(g => g.pipe(debounceTime(this.DEBOUNCE), switchMap(i => this.doSaveIngredient(i)))),
      takeUntilDestroyed(),
    ).subscribe();
  }

  ngOnInit() { this.loadCategories(); this.loadIngredients(); }

  loadCategories() { this.api.getCategories().subscribe(c => this.categories.set(c)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }

  countFor(categoryId: number | null): number {
    return this.ingredients().filter(i => i.categoryId === categoryId).length;
  }

  // ---- Gem-status-hjælpere ----
  status(prefix: 'c' | 'i', id: number): SaveStatus | undefined { return this.saveStatus()[prefix + id]; }
  private setStatus(key: string, s: SaveStatus | null) {
    const next = { ...this.saveStatus() };
    if (s === null) delete next[key]; else next[key] = s;
    this.saveStatus.set(next);
  }
  private markSaved(key: string) {
    this.setStatus(key, 'saved');
    // "Gemt ✓" fader stille væk efter et øjeblik.
    setTimeout(() => { if (this.saveStatus()[key] === 'saved') this.setStatus(key, null); }, 2200);
  }

  // ---- Kategorier ----
  addCategory() {
    if (!this.newCatName.trim()) return;
    // Nye kategorier lægges nederst (højeste rækkefølge + 10).
    const nextSort = Math.max(0, ...this.categories().map(c => c.sortOrder)) + 10;
    this.api.createCategory({ name: this.newCatName.trim(), sortOrder: nextSort })
      .subscribe({
        next: () => { this.newCatName = ''; this.loadCategories(); },
        error: () => this.toast.error('Kunne ikke oprette kategorien.'),
      });
  }

  // Debounced auto-gem: kaldes ved hvert tastetryk i kategori-navnet.
  queueCategory(c: Category) { this.setStatus('c' + c.id, 'saving'); this.catSave$.next(c); }
  // Straks-gem (fald-tilbage-knappen ved fejl).
  saveCategoryNow(c: Category) { this.setStatus('c' + c.id, 'saving'); this.doSaveCategory(c).subscribe(); }

  private doSaveCategory(c: Category) {
    const name = c.name.trim();
    if (!name) { this.setStatus('c' + c.id, 'error'); this.toast.error('Kategorinavn må ikke være tomt.'); return of(null); }
    return this.api.updateCategory(c.id, { name, sortOrder: c.sortOrder }).pipe(
      tap(() => { this.markSaved('c' + c.id); this.categories.set([...this.categories()]); }),
      catchError(() => {
        this.setStatus('c' + c.id, 'error');
        this.toast.error(`Kunne ikke gemme kategorien "${name}".`);
        return of(null);
      }),
    );
  }

  // Flyt kategori op/ned: ombyt i listen og gen-nummerér rækkefølgen (10, 20, 30 …).
  moveCategory(c: Category, dir: number) {
    const cats = [...this.categories()];
    const i = cats.findIndex(x => x.id === c.id);
    const j = i + dir;
    if (j < 0 || j >= cats.length) return;
    [cats[i], cats[j]] = [cats[j], cats[i]];
    this.setStatus('c' + c.id, 'saving');
    const calls = cats.map((cat, idx) =>
      this.api.updateCategory(cat.id, { name: cat.name.trim(), sortOrder: (idx + 1) * 10 }));
    forkJoin(calls).subscribe({
      next: () => { this.markSaved('c' + c.id); this.loadCategories(); },
      error: () => { this.setStatus('c' + c.id, 'error'); this.toast.error('Kunne ikke gemme rækkefølgen.'); },
    });
  }

  deleteCategory(c: Category) {
    if (!confirm(`Slet kategori "${c.name}"? Ingredienser beholdes uden kategori.`)) return;
    this.api.deleteCategory(c.id).subscribe({
      next: () => {
        if (this.filter() === c.id) this.filter.set('alle');
        this.loadCategories();
        this.loadIngredients();
      },
      error: () => this.toast.error(`Kunne ikke slette "${c.name}".`),
    });
  }

  // ---- Ingredienser ----
  addIngredient() {
    if (!this.newIngName.trim()) return;
    this.api.createIngredient({ name: this.newIngName.trim(), categoryId: this.newIngCat })
      .subscribe({
        next: () => { this.newIngName = ''; this.newIngCat = null; this.loadIngredients(); },
        error: () => this.toast.error('Kunne ikke oprette ingrediens.'),
      });
  }

  // Debounced auto-gem: kaldes ved navne- eller kategori-ændring på en ingrediens.
  queueIngredient(i: Ingredient) { this.setStatus('i' + i.id, 'saving'); this.ingSave$.next(i); }
  // Straks-gem (fald-tilbage-knappen ved fejl).
  saveIngredientNow(i: Ingredient) { this.setStatus('i' + i.id, 'saving'); this.doSaveIngredient(i).subscribe(); }

  private doSaveIngredient(i: Ingredient) {
    const name = i.name.trim();
    if (!name) { this.setStatus('i' + i.id, 'error'); this.toast.error('Ingrediensnavn må ikke være tomt.'); return of(null); }
    return this.api.updateIngredient(i.id, { name, categoryId: i.categoryId }).pipe(
      tap(() => { this.markSaved('i' + i.id); this.ingredients.set([...this.ingredients()]); }),
      catchError(() => {
        this.setStatus('i' + i.id, 'error');
        this.toast.error(`Kunne ikke gemme "${name}" (måske dublet-navn?).`);
        return of(null);
      }),
    );
  }

  deleteIngredient(i: Ingredient) {
    if (!confirm(`Slet ingrediens "${i.name}"?`)) return;
    this.api.deleteIngredient(i.id).subscribe({
      next: () => this.loadIngredients(),
      error: () => this.toast.error(`"${i.name}" er i brug (ret/varegruppe) og kan ikke slettes.`),
    });
  }
}
