import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Category, Ingredient, Recipe, RecipeUpsert, ItemGroup, ItemGroupUpsert,
  Week, WeekDetail, ShoppingList, Unit,
  CatalogRecipe, AdoptResult, ShareToken,
  RecipeShareTarget, SharedRecipe,
  HouseholdTask, TasksSummary, Store, Order, InviteResult
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

  // ----- Enheder (fri tekst) -----
  // Forslag til enheds-vælgeren: standard-sættet + de enheder husstanden allerede har brugt.
  getUnits(): Observable<string[]> { return this.http.get<string[]>(`${API}/units`); }

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

  // Opskrift-billede (valgfrit). Uploades som multipart; komprimeres yderligere server-side.
  uploadRecipeImage(id: number, image: Blob) {
    const form = new FormData();
    form.append('file', image, 'billede.jpg');
    return this.http.post<Recipe>(`${API}/recipes/${id}/image`, form);
  }
  deleteRecipeImage(id: number) { return this.http.delete(`${API}/recipes/${id}/image`); }

  // AI-scanning af opskrift-billede (valgfri feature). enabled() styrer om knappen vises;
  // scan() sender billedet og får et RecipeUpsert til gennemsyn (intet gemmes server-side).
  scanRecipeEnabled(): Observable<{ enabled: boolean }> { return this.http.get<{ enabled: boolean }>(`${API}/recipes/scan/enabled`); }
  scanRecipe(image: Blob): Observable<RecipeUpsert> {
    const form = new FormData();
    form.append('file', image, 'opskrift.jpg');
    return this.http.post<RecipeUpsert>(`${API}/recipes/scan`, form);
  }
  // Fulde URL'er til billed-endpoints (hentes med Bearer-token via secure-image-komponenten).
  recipeImageUrl(id: number) { return `${API}/recipes/${id}/image`; }
  catalogImageUrl(id: number) { return `${API}/catalog/recipes/${id}/image`; }

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
  markCooked(weekId: number, weekRecipeId: number) {
    return this.http.post<WeekDetail>(`${API}/weeks/${weekId}/recipes/${weekRecipeId}/cooked`, {});
  }
  unmarkCooked(weekId: number, weekRecipeId: number) {
    return this.http.delete<WeekDetail>(`${API}/weeks/${weekId}/recipes/${weekRecipeId}/cooked`);
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
  publishRecipe(id: number) { return this.http.post(`${API}/recipes/${id}/publish`, {}); }
  unpublishRecipe(id: number) { return this.http.delete(`${API}/recipes/${id}/publish`); }

  // ----- Selektiv deling af opskrifter (med én udvalgt modtager) -----
  shareRecipe(id: number, email: string) {
    return this.http.post<RecipeShareTarget>(`${API}/recipes/${id}/share`, { email });
  }
  unshareRecipe(id: number, targetHouseholdId: number) {
    return this.http.delete(`${API}/recipes/${id}/share/${targetHouseholdId}`);
  }
  getRecipeShares(id: number): Observable<RecipeShareTarget[]> {
    return this.http.get<RecipeShareTarget[]>(`${API}/recipes/${id}/shares`);
  }
  getSharedWithMe(): Observable<SharedRecipe[]> {
    return this.http.get<SharedRecipe[]>(`${API}/recipes/shared-with-me`);
  }
  adoptSharedRecipe(recipeId: number, body: { weekId?: number | null; servings?: number | null; dayOfWeek?: number | null } = {}) {
    return this.http.post<AdoptResult>(`${API}/recipes/shared-with-me/${recipeId}/adopt`, body);
  }
  // Billede for en opskrift delt TIL min husstand (Bearer sættes af interceptoren via secure-image).
  sharedRecipeImageUrl(recipeId: number) { return `${API}/recipes/shared-with-me/${recipeId}/image`; }

  // ----- Deling af indkøbsliste -----
  createShare(weekId: number) { return this.http.post<ShareToken>(`${API}/weeks/${weekId}/share`, {}); }
  revokeShare(weekId: number) { return this.http.delete(`${API}/weeks/${weekId}/share`); }
  // Anonyme kald (bruges af den offentlige del-side; token i URL'en er adgangen)
  getSharedList(token: string): Observable<ShoppingList> { return this.http.get<ShoppingList>(`${API}/share/${token}`); }
  setSharedCheck(token: string, lineKey: string, isChecked: boolean) {
    return this.http.put(`${API}/share/${token}/check`, { lineKey, isChecked });
  }

  // ----- Hjemmets opgaver -----
  getTasks(): Observable<HouseholdTask[]> { return this.http.get<HouseholdTask[]>(`${API}/tasks`); }
  getTasksSummary(): Observable<TasksSummary> { return this.http.get<TasksSummary>(`${API}/tasks/summary`); }
  createTask(body: { title: string; intervalDays?: number | null; assignees?: string[] | null }) {
    return this.http.post<HouseholdTask>(`${API}/tasks`, body);
  }
  updateTask(id: number, body: { title: string; intervalDays?: number | null; assignees?: string[] | null }) {
    return this.http.put<HouseholdTask>(`${API}/tasks/${id}`, body);
  }
  completeTask(id: number) { return this.http.post<HouseholdTask>(`${API}/tasks/${id}/complete`, {}); }
  uncompleteTask(id: number) { return this.http.post<HouseholdTask>(`${API}/tasks/${id}/uncomplete`, {}); }
  deleteTask(id: number) { return this.http.delete(`${API}/tasks/${id}`); }

  // ----- Ordrer: forbruger-side -----
  getStores(): Observable<Store[]> { return this.http.get<Store[]>(`${API}/orders/stores`); }
  getMyOrders(): Observable<Order[]> { return this.http.get<Order[]>(`${API}/orders`); }
  createOrderFromWeek(weekId: number, body: { storeName: string; note?: string | null }) {
    return this.http.post<Order>(`${API}/orders/from-week/${weekId}`, body);
  }
  cancelOrder(id: number) { return this.http.delete(`${API}/orders/${id}`); }
  // (Butiks-siden bor nu i den separate apps/supermarket-app.)

  // ----- Brugerkonti: invitér til husstanden (T2) -----
  // Returnerer et link som en eksisterende bruger kan dele, så andre kan oprette sig i samme husstand.
  createInvite(): Observable<InviteResult> { return this.http.post<InviteResult>(`${API}/auth/invite`, {}); }

  // ----- GDPR: data-eksport & -sletning -----
  // Henter hele husstandens data som JSON-objekt (Bearer-token sættes af interceptor).
  exportMyData(): Observable<unknown> { return this.http.get<unknown>(`${API}/privacy/export`); }
  // Sletter egen husstand permanent — kræver gen-indtastet adgangskode.
  deleteMyHousehold(password: string) { return this.http.post(`${API}/privacy/delete`, { password }); }
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
