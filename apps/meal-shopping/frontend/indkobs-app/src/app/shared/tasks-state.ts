import { Injectable, inject, signal } from '@angular/core';
import { Api } from '../api';

/**
 * Holder badge-tallet for Hjem-fanen (forfaldne pligter + åbne opgaver).
 * Opdateres ved app-start (hvis logget ind) og efter handlinger på Hjem-siden.
 */
@Injectable({ providedIn: 'root' })
export class TasksState {
  private api = inject(Api);
  readonly attention = signal(0); // vises som badge i navigationen

  refresh() {
    this.api.getTasksSummary().subscribe({
      next: s => this.attention.set(s.overdue + s.openTodos),
      error: () => this.attention.set(0) // fx 401 før login — vis bare intet
    });
  }
}
