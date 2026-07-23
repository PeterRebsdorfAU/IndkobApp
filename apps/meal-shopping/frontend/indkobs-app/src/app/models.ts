// Domæne-modeller der matcher backendens DTO'er.

// Enheder er FRI TEKST: brugeren kan skrive en hvilken som helst enhed (fx "glas",
// "kviste", "dåser"). Nedenstående er blot FORSLAG — ikke en lukket liste.
export type Unit = string;

// Standard-forslag (matcher backendens Units.Suggestions — små, menneskevenlige skrivemåder).
export const BASE_UNITS: string[] = [
  'stk', 'g', 'kg', 'ml', 'l', 'spsk', 'tsk', 'dåse', 'pakke', 'knivspids', 'bundt', 'fed',
];

// Bagudkompatibel {value,label}-liste (value = det der gemmes; label = visning, nu ens).
export const UNITS: { value: string; label: string }[] = BASE_UNITS.map(u => ({ value: u, label: u }));

// Ældre data (eller ikke-migrerede rækker) kan stadig have de gamle enum-navne ("Daase",
// "G" …); vis dem pænt. Egne/fri-tekst enheder vises uændret.
const LEGACY_LABELS: Record<string, string> = {
  Stk: 'stk', G: 'g', Kg: 'kg', Ml: 'ml', L: 'l', Spsk: 'spsk', Tsk: 'tsk',
  Daase: 'dåse', Pakke: 'pakke', Knivspids: 'knivspids', Bundt: 'bundt', Fed: 'fed',
};

export function unitLabel(u: Unit): string {
  return LEGACY_LABELS[u] ?? u;
}

// Fletter standard-forslag med husstandens tidligere brugte enheder (deduplikeret
// case-insensitivt, første skrivemåde vinder). Bruges til enheds-comboboxens datalist.
export function mergeUnitSuggestions(...lists: (string[] | undefined | null)[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const list of lists) {
    for (const raw of list ?? []) {
      const t = (raw ?? '').trim();
      if (!t) continue;
      const k = t.toLowerCase();
      if (!seen.has(k)) { seen.add(k); out.push(t); }
    }
  }
  return out;
}

export interface Category { id: number; name: string; sortOrder: number; }

export interface Ingredient { id: number; name: string; categoryId: number | null; categoryName: string | null; }

export interface IngredientLine {
  id: number; ingredientId: number; ingredientName: string;
  categoryName: string | null; quantity: number; unit: Unit;
}
export interface IngredientLineInput {
  ingredientId?: number | null; ingredientName: string; quantity: number; unit: Unit;
}

export interface Recipe {
  id: number; name: string; note: string | null; servings: number; ingredients: IngredientLine[];
  method: string | null; // valgfri fremgangsmåde (fritekst, evt. flere linjer)
  isPublic: boolean; // publiceret til den fælles inspirationsside?
  hasImage: boolean; // har et billede (hentes via GET /recipes/{id}/image)
}
export interface RecipeUpsert {
  name: string; note: string | null; servings: number; ingredients: IngredientLineInput[];
  method: string | null; // valgfri fremgangsmåde
}

export interface ItemGroup { id: number; name: string; ingredients: IngredientLine[]; }
export interface ItemGroupUpsert { name: string; ingredients: IngredientLineInput[]; }

export interface Week { id: number; year: number; weekNumber: number; }

export interface WeekRecipe {
  id: number; recipeId: number; recipeName: string;
  baseServings: number; servings: number | null; dayOfWeek: number | null;
  cookedUtc: string | null; // sat = markeret "lavet" (historik)
}
export interface WeekItemGroup { id: number; itemGroupId: number; itemGroupName: string; }
export interface WeekManualItem { id: number; ingredientId: number | null; name: string; quantity: number; unit: Unit; }

export interface WeekDetail {
  id: number; year: number; weekNumber: number;
  recipes: WeekRecipe[]; itemGroups: WeekItemGroup[]; manualItems: WeekManualItem[];
}

export interface ShoppingLine {
  lineKey: string; ingredientId: number | null; name: string;
  quantity: number; unit: Unit; isChecked: boolean; isManual: boolean; sources: string[];
}
export interface ShoppingCategoryGroup {
  categoryId: number | null; categoryName: string; sortOrder: number; lines: ShoppingLine[];
}
export interface ShoppingList {
  weekId: number; year: number; weekNumber: number; groups: ShoppingCategoryGroup[];
}

export const DAYS = ['Mandag', 'Tirsdag', 'Onsdag', 'Torsdag', 'Fredag', 'Lørdag', 'Søndag'];

export interface AuthResult {
  token: string; expiresUtc: string; householdId: number; householdName: string;
  // T2 (additive; null for ældre husstands-login uden individuel bruger):
  refreshToken?: string | null; displayName?: string | null; userId?: number | null;
}
export interface InviteResult { inviteToken: string; inviteLink: string; }

// ---------- Inspiration / katalog ----------
export interface CatalogLine { name: string; quantity: number; unit: Unit; }
export interface CatalogRecipe {
  id: number; title: string; note: string | null; servings: number;
  tags: string[]; ingredients: CatalogLine[];
  method: string | null; // valgfri fremgangsmåde (fritekst, evt. flere linjer)
  sharedBy: string | null; // husstand der har delt den (null = kurateret)
  hasImage: boolean; // har et billede (hentes via GET /catalog/recipes/{id}/image)
}
export interface AdoptResult { recipeId: number; recipeName: string; weekId: number | null; }

// ---------- Deling ----------
export interface ShareToken { token: string; }

// ---------- Selektiv deling af opskrifter ----------
// En modtager en egen opskrift er delt med (ejerens "delt med"-liste).
export interface RecipeShareTarget { targetHouseholdId: number; householdName: string; createdUtc: string; }
// En opskrift der er delt TIL min husstand (skrivebeskyttet + "Tilføj til mine").
export interface SharedRecipe {
  id: number; name: string; note: string | null; servings: number;
  ingredients: IngredientLine[]; method: string | null; hasImage: boolean;
  sharedByHouseholdName: string; createdUtc: string;
}

// ---------- Hjemmets opgaver ----------
export interface HouseholdTask {
  id: number; title: string;
  intervalDays: number | null;      // null = engangsopgave
  nextDueDate: string | null;       // "yyyy-MM-dd" (kun gentagne)
  assignees: string[]; currentAssignee: string | null;
  isDone: boolean; lastCompletedUtc: string | null;
}
export interface TasksSummary { overdue: number; openTodos: number; }

// ---------- Ordrer (butiks-flow) ----------
export interface Store { name: string; }
export interface OrderLine {
  id: number; name: string; quantity: number; unit: Unit;
  categoryName: string | null; isPacked: boolean; notAvailable: boolean;
}
export interface Order {
  id: number; householdName: string; storeName: string; status: string;
  note: string | null; createdUtc: string; readyUtc: string | null; lines: OrderLine[];
}
