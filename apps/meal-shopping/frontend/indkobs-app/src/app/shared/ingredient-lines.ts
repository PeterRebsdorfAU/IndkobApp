import { Component, input, model, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Ingredient, IngredientLineInput, BASE_UNITS, mergeUnitSuggestions } from '../models';

/**
 * Genbrugelig editor til ingredienslinjer (bruges af både retter og varegrupper).
 * Linjerne tovejs-bindes via `lines`. Ingrediensnavne autoudfyldes fra master-listen.
 *
 * Enheds-feltet er en COMBOBOX (`<input list=...>`): brugeren kan vælge en kendt enhed
 * ELLER skrive sin egen (fri tekst). Forslagene er standard-sættet + husstandens tidligere
 * brugte enheder (sendes ind via `unitSuggestions`).
 */
@Component({
  selector: 'ingredient-lines',
  imports: [FormsModule],
  template: `
    <label>Ingredienser</label>
    @for (line of lines(); track $index) {
      <div class="line-input">
        <input class="full" list="ing-options" placeholder="Ingrediens"
               [(ngModel)]="line.ingredientName" [name]="'ing' + $index" />
        <input type="number" min="0" step="0.001" placeholder="Antal"
               [(ngModel)]="line.quantity" [name]="'qty' + $index" />
        <input list="unit-options" placeholder="Enhed" autocomplete="off"
               [(ngModel)]="line.unit" [name]="'unit' + $index" />
        <button type="button" class="danger" (click)="remove($index)" title="Fjern">✕</button>
      </div>
    }
    <datalist id="ing-options">
      @for (i of ingredients(); track i.id) { <option [value]="i.name"></option> }
    </datalist>
    <datalist id="unit-options">
      @for (u of unitOptions(); track u) { <option [value]="u"></option> }
    </datalist>
    <button type="button" class="small" (click)="add()">+ Tilføj ingrediens</button>
  `
})
export class IngredientLinesEditor {
  lines = model<IngredientLineInput[]>([]);
  ingredients = input<Ingredient[]>([]);
  // Husstandens tidligere brugte enheder (afledt af data); flettes med standard-forslagene.
  unitSuggestions = input<string[]>([]);

  unitOptions = computed(() => mergeUnitSuggestions(BASE_UNITS, this.unitSuggestions()));

  add() {
    this.lines.update(l => [...l, { ingredientName: '', quantity: 1, unit: 'stk' }]);
  }
  remove(i: number) {
    this.lines.update(l => l.filter((_, idx) => idx !== i));
  }
}
