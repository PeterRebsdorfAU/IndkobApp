import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Category, Ingredient, Recipe, RecipeUpsert, ItemGroup, ItemGroupUpsert,
  Week, WeekDetail, ShoppingList, Unit,
  CatalogRecipe, AdoptResult, PantryItem, ShareToken
} from './models';
import { environment } from '../environments/environment';

// Backend-base-URL:
//  - I produktion (Render) bruges environment.apiBase (den faste backend-URL).
//  - Lokalt (apiBase = '') bruges SAMME vært som frontenden hentes fra, så appen
//    virker både på PC (localhost) og telefon (PC'ens LAN-IP) på port 5298.
const API_PORT = 5298;
const API = environment.apiBase || `${location.protocol}//${location.hostname}:${API_PORT}/api`;

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);

  // ----- Kategorier -----
  getCategories(): Observable<Category[]> { return this.http.get<Category[]>(`${API}/categories`); }
  createCategory(c: { name: string; sortOrder: number }) { return this.http.post<Category>(`${API}/categories`, { id: 0, ...c }); }
  updateCategory(id: number, c: { name: string; sortOrder: number }) { return this.http.put(`${API}/categories/${id}`, { id, ...c }); }
  deleteCategory(id: number) { return this.http.delete(`${API}/categories/${id}`); }

  // ----- Ingredienser -----
  getIngredients(): Observable<Ingredient[]> { return this.http.get<Ingredient[]>(`${API}/ingredients`); }
  createIngredient(i: { name: string; categoryId: number | null }) { return this.http.post<Ingredient>(`${API}/ingredients`, i); }
  updateIngredient(id: number, i: { name: string; categoryId: number | null }) { return this.http.put(`${API}/ingredients/${id}`, i); }
  deleteIngredient(id: number) { return this.http.delete(`${API}/ingredients/${id}`); }

  // ----- Retter -----
  getRecipes(): Observable<Recipe[]> { return this.http.get<Recipe[]>(`${API}/recipes`); }
  getRecipe(id: number): Observable<Recipe> { return this.http.get<Recipe>(`${API}/recipes/${id}`); }
  createRecipe(r: RecipeUpsert) { return this.http.post<Recipe>(`${API}/recipes`, r); }
  updateRecipe(id: number, r: RecipeUpsert) { return this.http.put<Recipe>(`${API}/recipes/${id}`, r); }
  deleteRecipe(id: number) { return this.http.delete(`${API}/recipes/${id}`); }

  // ----- Varegrupper -----
  getItemGroups(): Observable<ItemGroup[]> { return this.http.get<ItemGroup[]>(`${API}/item-groups`); }
  createItemGroup(g: ItemGroupUpsert) { return this.http.post<ItemGroup>(`${API}/item-groups`, g); }
  updateItemGroup(id: number, g: ItemGroupUpsert) { return this.http.put<ItemGroup>(`${API}/item-groups/${id}`, g); }
  deleteItemGroup(id: number) { return this.http.delete(`${API}/item-groups/${id}`); }

  // ----- Uger -----
  getWeeks(): Observable<Week[]> { return this.http.get<Week[]>(`${API}/weeks`); }
  getWeek(id: number): Observable<WeekDetail> { return this.http.get<WeekDetail>(`${API}/weeks/${id}`); }
  createWeek(w: { year: number; weekNumber: number }) { return this.http.post<Week>(`${API}/weeks`, w); }
  deleteWeek(id: number) { return this.http.delete(`${API}/weeks/${id}`); }

  addWeekRecipe(weekId: number, body: { recipeId: number; servings?: number | null; dayOfWeek?: number | null }) {
    return this.http.post<WeekDetail>(`${API}/weeks/${weekId}/recipes`, body);
  }
  updateWeekRecipe(weekId: number, weekRecipeId: number, body: { servings: number | null; dayOfWeek: number | null }) {
    return this.http.put<WeekDetail>(`${API}/weeks/${weekId}/recipes/${weekRecipeId}`, body);
  }
  removeWeekRecipe(weekId: number, weekRecipeId: number) {
    return this.http.delete<WeekDetail>(`${API}/weeks/${weekId}/recipes/${weekRecipeId}`);
  }
  addWeekItemGroup(weekId: number, itemGroupId: number) {
    return this.http.post<WeekDetail>(`${API}/weeks/${weekId}/item-groups`, { itemGroupId });
  }
  removeWeekItemGroup(weekId: number, weekItemGroupId: number) {
    return this.http.delete<WeekDetail>(`${API}/weeks/${weekId}/item-groups/${weekItemGroupId}`);
  }
  addWeekManualItem(weekId: number, body: { ingredientId?: number | null; freeText?: string | null; quantity: number; unit: Unit }) {
    return this.http.post<WeekDetail>(`${API}/weeks/${weekId}/manual-items`, body);
  }
  removeWeekManualItem(weekId: number, manualItemId: number) {
    return this.http.delete<WeekDetail>(`${API}/weeks/${weekId}/manual-items/${manualItemId}`);
  }

  // ----- Indkøbsliste -----
  getShoppingList(weekId: number): Observable<ShoppingList> { return this.http.get<ShoppingList>(`${API}/weeks/${weekId}/shopping-list`); }
  setCheck(weekId: number, lineKey: string, isChecked: boolean) {
    return this.http.put(`${API}/weeks/${weekId}/shopping-list/check`, { lineKey, isChecked });
  }

  // ----- Inspiration / katalog -----
  getCatalog(): Observable<CatalogRecipe[]> { return this.http.get<CatalogRecipe[]>(`${API}/catalog/recipes`); }
  adoptCatalogRecipe(id: number, body: { weekId?: number | null; servings?: number | null; dayOfWeek?: number | null }) {
    return this.http.post<AdoptResult>(`${API}/catalog/recipes/${id}/adopt`, body);
  }

  // ----- Køkkenlager -----
  getPantry(): Observable<PantryItem[]> { return this.http.get<PantryItem[]>(`${API}/pantry`); }
  addPantryItem(body: { ingredientId?: number | null; ingredientName?: string | null; quantity: number; unit: Unit }) {
    return this.http.post<PantryItem>(`${API}/pantry`, body);
  }
  updatePantryItem(id: number, body: { quantity: number; unit: Unit }) {
    return this.http.put(`${API}/pantry/${id}`, body);
  }
  deletePantryItem(id: number) { return this.http.delete(`${API}/pantry/${id}`); }

  // ----- Deling af indkøbsliste -----
  createShare(weekId: number) { return this.http.post<ShareToken>(`${API}/weeks/${weekId}/share`, {}); }
  revokeShare(weekId: number) { return this.http.delete(`${API}/weeks/${weekId}/share`); }
  // Anonyme kald (bruges af den offentlige del-side; token i URL'en er adgangen)
  getSharedList(token: string): Observable<ShoppingList> { return this.http.get<ShoppingList>(`${API}/share/${token}`); }
  setSharedCheck(token: string, lineKey: string, isChecked: boolean) {
    return this.http.put(`${API}/share/${token}/check`, { lineKey, isChecked });
  }
}

// ISO-ugenummer for en given dato (til uge-vælgeren).
export function isoWeek(date: Date): { year: number; week: number } {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
  const dayNum = (d.getUTCDay() + 6) % 7; // mandag=0
  d.setUTCDate(d.getUTCDate() - dayNum + 3); // torsdag i denne uge
  const firstThursday = new Date(Date.UTC(d.getUTCFullYear(), 0, 4));
  const firstDayNum = (firstThursday.getUTCDay() + 6) % 7;
  firstThursday.setUTCDate(firstThursday.getUTCDate() - firstDayNum + 3);
  const week = 1 + Math.round((d.getTime() - firstThursday.getTime()) / (7 * 24 * 3600 * 1000));
  return { year: d.getUTCFullYear(), week };
}
