// Minimale typer for butiks-appen (matcher backendens ordre-DTO'er).
export type Unit =
  | 'Stk' | 'G' | 'Kg' | 'Ml' | 'L'
  | 'Spsk' | 'Tsk' | 'Daase' | 'Pakke' | 'Knivspids' | 'Bundt' | 'Fed';

const UNIT_LABELS: Record<Unit, string> = {
  Stk: 'stk', G: 'g', Kg: 'kg', Ml: 'ml', L: 'l', Spsk: 'spsk', Tsk: 'tsk',
  Daase: 'dåse', Pakke: 'pakke', Knivspids: 'knivspids', Bundt: 'bundt', Fed: 'fed'
};
export function unitLabel(u: Unit): string { return UNIT_LABELS[u] ?? u; }

export interface Store { name: string; }

export interface OrderLine {
  id: number; name: string; quantity: number; unit: Unit;
  categoryName: string | null; isPacked: boolean; notAvailable: boolean;
}
export interface Order {
  id: number; householdName: string; storeName: string; status: string;
  note: string | null; createdUtc: string; readyUtc: string | null; lines: OrderLine[];
}
