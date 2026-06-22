import { Component, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Ingredient, IngredientLineInput, UNITS } from '../models';

/**
 * Genbrugelig editor til ingredienslinjer (bruges af både retter og varegrupper).
 * Linjerne tovejs-bindes via `lines`. Ingrediensnavne autoudfyldes fra master-listen.
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
        <select [(ngModel)]="line.unit" [name]="'unit' + $index">
          @for (u of units; track u.value) { <option [value]="u.value">{{ u.label }}</option> }
        </select>
        <button type="button" class="danger" (click)="remove($index)" title="Fjern">✕</button>
      </div>
    }
    <datalist id="ing-options">
      @for (i of ingredients(); track i.id) { <option [value]="i.name"></option> }
    </datalist>
    <button type="button" class="small" (click)="add()">+ Tilføj ingrediens</button>
  `
})
export class IngredientLinesEditor {
  lines = model<IngredientLineInput[]>([]);
  ingredients = input<Ingredient[]>([]);
  units = UNITS;

  add() {
    this.lines.update(l => [...l, { ingredientName: '', quantity: 1, unit: 'Stk' }]);
  }
  remove(i: number) {
    this.lines.update(l => l.filter((_, idx) => idx !== i));
  }
}
