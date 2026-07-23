// Domæne-modeller der matcher backendens DTO'er.

// Enhedsnavne SKAL matche C#-enum'en Unit (serialiseres som tekst).
export type Unit =
  | 'Stk' | 'G' | 'Kg' | 'Ml' | 'L'
  | 'Spsk' | 'Tsk' | 'Daase' | 'Pakke' | 'Knivspids' | 'Bundt' | 'Fed';

// Visningslabels (fx "Dåse" i stedet for enum-navnet "Daase").
export const UNITS: { value: Unit; label: string }[] = [
  { value: 'Stk', label: 'stk' },
  { value: 'G', label: 'g' },
  { value: 'Kg', label: 'kg' },
  { value: 'Ml', label: 'ml' },
  { value: 'L', label: 'l' },
  { value: 'Spsk', label: 'spsk' },
  { value: 'Tsk', label: 'tsk' },
  { value: 'Daase', label: 'dåse' },
  { value: 'Pakke', label: 'pakke' },
  { value: 'Knivspids', label: 'knivspids' },
  { value: 'Bundt', label: 'bundt' },
  { value: 'Fed', label: 'fed' },
];

export function unitLabel(u: Unit): string {
  return UNITS.find(x => x.value === u)?.label ?? u;
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
}
export interface AdoptResult { recipeId: number; recipeName: string; weekId: number | null; }

// ---------- Deling ----------
export interface ShareToken { token: string; }

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
