import { Injectable, signal } from '@angular/core';

const KEY = 'indkobs.selectedWeekId';

/** Holder styr på hvilken uge der er valgt, delt mellem Uge- og Indkøbs-siden. */
@Injectable({ providedIn: 'root' })
export class WeekState {
  readonly selectedWeekId = signal<number | null>(this.read());

  select(id: number | null) {
    this.selectedWeekId.set(id);
    if (id == null) localStorage.removeItem(KEY);
    else localStorage.setItem(KEY, String(id));
  }

  private read(): number | null {
    const raw = localStorage.getItem(KEY);
    return raw ? Number(raw) : null;
  }
}
