import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { WeekState } from '../shared/week-state';
import { Recipe, RecipeUpsert, Ingredient, IngredientLineInput, CatalogRecipe, RecipeShareTarget, SharedRecipe, unitLabel } from '../models';
import { IngredientLinesEditor } from '../shared/ingredient-lines';
import { EmptyState } from '../shared/empty-state';
import { SecureImage } from '../shared/secure-image';
import { downscaleImage } from '../shared/image-util';
import { ToastService } from '../shared/toast';

@Component({
  selector: 'page-recipes',
  imports: [FormsModule, IngredientLinesEditor, EmptyState, SecureImage],
  template: `
    <div class="hero">
      <span class="eyebrow">Retter</span>
      <div class="hero-title">Dine retter</div>
      <div class="hero-sub">Opret egne retter — eller find inspiration i kataloget.</div>
    </div>

    <!-- Faner: egne retter vs. inspiration fra kataloget -->
    <div class="segment" style="width:100%; margin-bottom:.9rem">
      <button [class.active]="tab() === 'mine'" (click)="tab.set('mine')">Mine retter</button>
      <button [class.active]="tab() === 'inspiration'" (click)="tab.set('inspiration')">Inspiration</button>
      <button [class.active]="tab() === 'delt'" (click)="tab.set('delt')">
        Delt med mig @if (sharedWithMe().length) { <span class="badge">{{ sharedWithMe().length }}</span> }
      </button>
    </div>

    @if (tab() === 'mine') {
      @if (!editing()) {
        <button class="primary" (click)="startNew()">+ Ny ret</button>

        @for (r of recipes(); track r.id) {
          <div class="card">
            @if (r.hasImage) {
              <secure-image [src]="recipeImageUrl(r.id)" [alt]="r.name" />
            }
            <div class="spread">
              <div class="grow">
                <h3>{{ r.name }} @if (r.isPublic) { <span class="badge">Offentlig</span> }</h3>
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
            @if (r.method) {
              <div style="margin-top:.6rem">
                <div class="muted" style="font-weight:600;margin-bottom:.2rem">Fremgangsmåde</div>
                <div style="white-space:pre-wrap">{{ r.method }}</div>
              </div>
            }
            <div class="row-wrap" style="margin-top:.5rem">
              @if (!r.isPublic) {
                <button class="small" (click)="publish(r)">Del på Inspiration</button>
              } @else {
                <button class="small" (click)="unpublish(r)">Fjern fra Inspiration</button>
              }
              <button class="small" (click)="toggleShare(r)">
                {{ shareOpen() === r.id ? 'Skjul deling' : 'Del med…' }}
              </button>
            </div>

            @if (shareOpen() === r.id) {
              <div class="card accent" style="margin-top:.6rem">
                <div class="muted" style="font-weight:600;margin-bottom:.35rem">Del "{{ r.name }}" med en bestemt husstand</div>
                <div class="row">
                  <input class="grow" type="email" placeholder="Modtagerens login-email"
                    [ngModel]="shareEmail()" (ngModelChange)="shareEmail.set($event)"
                    (keyup.enter)="doShare(r)" />
                  <button class="primary small" (click)="doShare(r)" [disabled]="sharing()">Del</button>
                </div>
                <div class="muted" style="font-size:.8rem;margin-top:.3rem">
                  Kun den valgte husstand kan se opskriften under "Delt med mig".
                </div>

                @if (shares().length) {
                  <div style="margin-top:.6rem">
                    <div class="muted" style="font-weight:600;margin-bottom:.25rem">Delt med</div>
                    @for (s of shares(); track s.targetHouseholdId) {
                      <div class="spread" style="margin-bottom:.25rem">
                        <span class="pill">{{ s.householdName }}</span>
                        <button class="small danger" (click)="doUnshare(r, s)">Fjern</button>
                      </div>
                    }
                  </div>
                } @else {
                  <div class="muted" style="font-size:.82rem;margin-top:.5rem">Endnu ikke delt med nogen.</div>
                }
              </div>
            }
          </div>
        } @empty {
          <app-empty-state icon="🍽️" title="Ingen retter endnu"
            text="Opret din første ret — eller hent inspiration fra det fælles katalog.">
            <button class="primary" (click)="startNew()">+ Ny ret</button>
            <button (click)="tab.set('inspiration')">✨ Se Inspiration</button>
          </app-empty-state>
        }
      }

      @if (editing(); as form) {
        <div class="card">
          <h2>{{ form.id ? 'Rediger ret' : 'Ny ret' }}</h2>

          @if (scanEnabled()) {
            <!-- AI-scanning: udfyld felterne fra et foto af en opskrift (kun forudfyldning; intet gemmes) -->
            <div class="field">
              <button type="button" class="small" (click)="scanInput.click()" [disabled]="scanning()">
                {{ scanning() ? '📷 Læser billedet…' : '📷 Scan opskrift' }}
              </button>
              <input #scanInput type="file" accept="image/*" hidden (change)="onScanPicked($event)" />
              <div class="muted" style="font-size:.8rem;margin-top:.3rem">
                Vælg et foto af en opskrift — AI læser titel, ingredienser og fremgangsmåde.
                Gennemse og ret felterne inden du gemmer.
              </div>
            </div>
          }

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
          <div class="field">
            <label>Fremgangsmåde (valgfri)</label>
            <textarea [(ngModel)]="form.method" rows="6"
              placeholder="Beskriv trinene, fx: 1) Brun kødet. 2) Tilsæt løg og hvidløg. 3) Lad simre 15 min."></textarea>
          </div>

          <div class="field">
            <label>Billede (valgfrit)</label>
            @if (pendingPreview(); as p) {
              <!-- Nyt, valgt billede (forhåndsvisning inden upload) -->
              <img [src]="p" alt="Forhåndsvisning" class="recipe-image" />
              <div class="row" style="margin-top:.4rem">
                <button type="button" class="small" (click)="clearPendingImage()">Fjern valgt billede</button>
              </div>
            } @else if (form.id && editImageState() === 'keep') {
              <!-- Eksisterende billede (vises kun ved redigering) -->
              <secure-image [src]="recipeImageUrl(form.id)" alt="Nuværende billede" />
              <div class="row" style="margin-top:.4rem">
                <button type="button" class="small danger" (click)="editImageState.set('remove')">Fjern billede</button>
              </div>
            } @else if (editImageState() === 'remove') {
              <div class="muted">Billedet fjernes når du gemmer.</div>
              <div class="row" style="margin-top:.4rem">
                <button type="button" class="small" (click)="editImageState.set('keep')">Fortryd</button>
              </div>
            }
            <input type="file" accept="image/*" (change)="onImagePicked($event)" style="margin-top:.4rem" />
            <div class="muted" style="font-size:.8rem;margin-top:.3rem">
              Billedet skaleres automatisk ned, så det fylder lidt.
            </div>
          </div>

          <ingredient-lines [(lines)]="form.ingredients" [ingredients]="ingredients()" [unitSuggestions]="units()" />

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
        <input placeholder="Søg titel, tag eller ingrediens…" [ngModel]="query()" (ngModelChange)="query.set($event)" />
      </div>

      @if (added()) { <div class="card accent">{{ added() }}</div> }

      @for (c of filteredCatalog(); track c.id) {
        <div class="card">
          @if (c.hasImage) {
            <secure-image [src]="catalogImageUrl(c.id)" [alt]="c.title" />
          }
          <div class="spread">
            <div class="grow">
              <h3>{{ c.title }}</h3>
              <div class="muted">{{ c.servings }} pers.
                @for (t of c.tags; track t) { · <span>{{ t }}</span> }
                @if (c.sharedBy) { · <span>delt af {{ c.sharedBy }}</span> }
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
          @if (c.method) {
            <div style="margin-top:.6rem">
              <div class="muted" style="font-weight:600;margin-bottom:.2rem">Fremgangsmåde</div>
              <div style="white-space:pre-wrap">{{ c.method }}</div>
            </div>
          }
        </div>
      } @empty {
        <div class="empty">Ingen opskrifter matcher søgningen.</div>
      }
    }

    @if (tab() === 'delt') {
      <p class="muted">Opskrifter en anden husstand har delt direkte med jer. Tryk "Tilføj til mine"
        for at kopiere en over til dine egne retter.</p>

      @if (added()) { <div class="card accent">{{ added() }}</div> }

      @for (s of sharedWithMe(); track s.id) {
        <div class="card">
          @if (s.hasImage) {
            <secure-image [src]="sharedImageUrl(s.id)" [alt]="s.name" />
          }
          <div class="spread">
            <div class="grow">
              <h3>{{ s.name }}</h3>
              <div class="muted">{{ s.servings }} pers. · {{ s.ingredients.length }} ingredienser · delt af {{ s.sharedByHouseholdName }}</div>
            </div>
            <button class="primary small" (click)="adoptShared(s)" [disabled]="adopting() === s.id">
              {{ adopting() === s.id ? '…' : '+ Tilføj til mine' }}
            </button>
          </div>
          @if (s.note) { <div class="muted" style="margin-top:.4rem">{{ s.note }}</div> }
          <div class="row-wrap" style="margin-top:.5rem">
            @for (i of s.ingredients; track i.id) {
              <span class="pill">{{ i.ingredientName }} {{ i.quantity }} {{ label(i.unit) }}</span>
            }
          </div>
          @if (s.method) {
            <div style="margin-top:.6rem">
              <div class="muted" style="font-weight:600;margin-bottom:.2rem">Fremgangsmåde</div>
              <div style="white-space:pre-wrap">{{ s.method }}</div>
            </div>
          }
        </div>
      } @empty {
        <app-empty-state icon="🤝" title="Ingen delte opskrifter endnu"
          text="Når en anden husstand deler en opskrift direkte med jer, dukker den op her.">
        </app-empty-state>
      }
    }
  `
})
export class RecipesPage implements OnInit {
  private api = inject(Api);
  private weekState = inject(WeekState);
  private toast = inject(ToastService);

  // AI-scanning af opskrift-billede: kun tilgængelig når backenden har en Gemini-nøgle.
  scanEnabled = signal(false);
  scanning = signal(false);

  recipes = signal<Recipe[]>([]);
  ingredients = signal<Ingredient[]>([]);
  units = signal<string[]>([]);
  editing = signal<(RecipeUpsert & { id: number | null }) | null>(null);
  error = signal('');
  saving = signal(false);

  // Billede-tilstand i editoren:
  //  - pendingImage/pendingPreview: et nyt (nedskaleret) billede valgt men ikke uploadet endnu.
  //  - editImageState: for et EKSISTERENDE billede — beholdes ('keep') eller fjernes ('remove') ved gem.
  pendingImage = signal<Blob | null>(null);
  pendingPreview = signal<string | null>(null);
  editImageState = signal<'keep' | 'remove'>('keep');

  recipeImageUrl = (id: number) => this.api.recipeImageUrl(id);
  catalogImageUrl = (id: number) => this.api.catalogImageUrl(id);

  // Inspiration + selektiv deling
  tab = signal<'mine' | 'inspiration' | 'delt'>('mine');
  catalog = signal<CatalogRecipe[]>([]);

  // Selektiv deling: "Del med…"-panel pr. egen opskrift.
  shareOpen = signal<number | null>(null);   // hvilken opskrift har panelet åbent
  shareEmail = signal('');
  shares = signal<RecipeShareTarget[]>([]);   // modtagere for den åbne opskrift
  sharing = signal(false);
  // "Delt med mig": opskrifter delt TIL min husstand.
  sharedWithMe = signal<SharedRecipe[]>([]);
  sharedImageUrl = (id: number) => this.api.sharedRecipeImageUrl(id);
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
    this.loadUnits();
    // Er AI-scanning slået til på serveren? (Skjuler knappen når featuren er i dvale.)
    this.api.scanRecipeEnabled().subscribe({
      next: r => this.scanEnabled.set(r.enabled),
      error: () => this.scanEnabled.set(false)
    });
    this.api.getCatalog().subscribe(c => this.catalog.set(c));
    this.loadSharedWithMe();
    // Vis hvilken uge "Tilføj" lægger retten på (den senest valgte uge).
    const wid = this.weekState.selectedWeekId();
    if (wid) this.api.getWeek(wid).subscribe(w => this.weekLabel.set(`uge ${w.weekNumber}, ${w.year}`));
  }

  load() { this.api.getRecipes().subscribe(r => this.recipes.set(r)); }
  loadIngredients() { this.api.getIngredients().subscribe(i => this.ingredients.set(i)); }
  loadUnits() { this.api.getUnits().subscribe(u => this.units.set(u)); }

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
    this.resetImageState();
    this.editing.set({ id: null, name: '', note: '', servings: 4, ingredients: [], method: '' });
  }

  edit(r: Recipe) {
    this.error.set('');
    this.resetImageState();
    const ingredients: IngredientLineInput[] = r.ingredients.map(i => ({
      ingredientId: i.ingredientId, ingredientName: i.ingredientName, quantity: i.quantity, unit: i.unit
    }));
    this.editing.set({ id: r.id, name: r.name, note: r.note ?? '', servings: r.servings, ingredients, method: r.method ?? '' });
  }

  cancel() { this.resetImageState(); this.editing.set(null); }

  // ---------- Billede i editoren ----------
  async onImagePicked(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // tillad at vælge samme fil igen senere
    if (!file) return;
    let blob: Blob = file;
    try {
      blob = await downscaleImage(file); // nedskalér klient-side (serveren komprimerer også)
    } catch {
      // Kunne ikke nedskalere (fx eksotisk format) — upload originalen, serveren håndterer den.
    }
    this.clearPendingImage();
    this.pendingImage.set(blob);
    this.pendingPreview.set(URL.createObjectURL(blob));
    this.editImageState.set('keep');
  }

  clearPendingImage() {
    const url = this.pendingPreview();
    if (url) URL.revokeObjectURL(url);
    this.pendingImage.set(null);
    this.pendingPreview.set(null);
  }

  private resetImageState() {
    this.clearPendingImage();
    this.editImageState.set('keep');
  }

  // ---------- AI-scanning: forudfyld editoren fra et foto ----------
  async onScanPicked(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // tillad at vælge samme fil igen
    if (!file || !this.editing()) return;

    let blob: Blob = file;
    try {
      blob = await downscaleImage(file); // nedskalér klient-side (serveren komprimerer også)
    } catch {
      // Kunne ikke nedskalere — send originalen, serveren håndterer den.
    }

    this.scanning.set(true);
    this.api.scanRecipe(blob).subscribe({
      next: scanned => {
        this.scanning.set(false);
        const f = this.editing();
        if (!f) return;
        // Byg et nyt form-objekt så child-editoren (ingredient-lines) genbinder linjerne.
        this.editing.set({
          ...f,
          name: scanned.name?.trim() || f.name,
          servings: scanned.servings || f.servings,
          method: scanned.method ?? f.method,
          ingredients: (scanned.ingredients?.length ? scanned.ingredients : f.ingredients).map(l => ({
            ingredientId: l.ingredientId ?? null,
            ingredientName: l.ingredientName,
            quantity: l.quantity,
            unit: l.unit
          }))
        });
        this.toast.success('Opskriften er læst ind — gennemse og ret felterne inden du gemmer.');
      },
      error: () => {
        this.scanning.set(false);
        this.toast.error('Kunne ikke læse opskriften fra billedet. Prøv igen eller et andet foto.');
      }
    });
  }

  save() {
    const form = this.editing();
    if (!form) return;
    if (!form.name.trim()) { this.error.set('Navn skal udfyldes.'); return; }
    const payload: RecipeUpsert = {
      name: form.name.trim(),
      note: form.note?.trim() || null,
      servings: Number(form.servings) || 1,
      ingredients: form.ingredients.filter(l => l.ingredientName.trim()),
      method: form.method?.trim() || null
    };
    this.saving.set(true);
    const obs = form.id
      ? this.api.updateRecipe(form.id, payload)
      : this.api.createRecipe(payload);
    obs.subscribe({
      next: saved => {
        // Gem billedet EFTER opskriften findes (endpointet kræver et opskrift-id).
        this.persistImage(saved.id).then(() => {
          this.saving.set(false);
          this.editing.set(null);
          this.resetImageState();
          this.load();
          this.loadIngredients();
        });
      },
      error: () => { this.saving.set(false); this.error.set('Kunne ikke gemme. Kører backend?'); }
    });
  }

  // Uploader et nyt billede, eller sletter det eksisterende — afhængigt af editorens tilstand.
  private async persistImage(recipeId: number): Promise<void> {
    const pending = this.pendingImage();
    if (pending) {
      await new Promise<void>(resolve =>
        this.api.uploadRecipeImage(recipeId, pending).subscribe({ next: () => resolve(), error: () => resolve() }));
    } else if (this.editImageState() === 'remove') {
      await new Promise<void>(resolve =>
        this.api.deleteRecipeImage(recipeId).subscribe({ next: () => resolve(), error: () => resolve() }));
    }
  }

  remove(r: Recipe) {
    if (!confirm(`Slet "${r.name}"?`)) return;
    this.api.deleteRecipe(r.id).subscribe(() => this.load());
  }

  // Publicér/fjern fra den fælles inspirationsside (snapshot — gen-publicér for at opdatere).
  publish(r: Recipe) {
    if (!confirm(`Del "${r.name}" på Inspiration, så ALLE husstande kan se og bruge den?`)) return;
    this.api.publishRecipe(r.id).subscribe(() => {
      this.load();
      this.api.getCatalog().subscribe(c => this.catalog.set(c));
    });
  }

  unpublish(r: Recipe) {
    if (!confirm(`Fjern "${r.name}" fra Inspiration?`)) return;
    this.api.unpublishRecipe(r.id).subscribe(() => {
      this.load();
      this.api.getCatalog().subscribe(c => this.catalog.set(c));
    });
  }

  // ---------- Selektiv deling: del én opskrift med én udvalgt modtager ----------
  toggleShare(r: Recipe) {
    if (this.shareOpen() === r.id) { this.shareOpen.set(null); return; }
    this.shareOpen.set(r.id);
    this.shareEmail.set('');
    this.shares.set([]);
    this.api.getRecipeShares(r.id).subscribe(list => this.shares.set(list));
  }

  doShare(r: Recipe) {
    const email = this.shareEmail().trim();
    if (!email) { this.toast.error('Skriv modtagerens email.'); return; }
    this.sharing.set(true);
    this.api.shareRecipe(r.id, email).subscribe({
      next: target => {
        this.sharing.set(false);
        this.shareEmail.set('');
        this.toast.success(`"${r.name}" er delt med ${target.householdName}.`);
        this.api.getRecipeShares(r.id).subscribe(list => this.shares.set(list));
      },
      error: err => {
        this.sharing.set(false);
        this.toast.error(err?.status === 404 ? 'Ingen konto med den email.' : 'Kunne ikke dele opskriften.');
      }
    });
  }

  doUnshare(r: Recipe, s: RecipeShareTarget) {
    this.api.unshareRecipe(r.id, s.targetHouseholdId).subscribe({
      next: () => {
        this.toast.success(`Deling med ${s.householdName} er fjernet.`);
        this.api.getRecipeShares(r.id).subscribe(list => this.shares.set(list));
      },
      error: () => this.toast.error('Kunne ikke fjerne delingen.')
    });
  }

  // ---------- "Delt med mig": adoptér en opskrift delt til min husstand ----------
  loadSharedWithMe() {
    this.api.getSharedWithMe().subscribe(list => this.sharedWithMe.set(list));
  }

  adoptShared(s: SharedRecipe) {
    this.adopting.set(s.id);
    this.added.set('');
    this.api.adoptSharedRecipe(s.id).subscribe({
      next: res => {
        this.adopting.set(null);
        this.added.set(`"${res.recipeName}" er tilføjet til dine retter. Læg den på en uge for at få den på indkøbslisten.`);
        this.load();
        this.loadIngredients();
        this.toast.success(`"${res.recipeName}" er tilføjet til dine retter.`);
      },
      error: () => { this.adopting.set(null); this.toast.error('Kunne ikke tilføje opskriften.'); }
    });
  }
}
