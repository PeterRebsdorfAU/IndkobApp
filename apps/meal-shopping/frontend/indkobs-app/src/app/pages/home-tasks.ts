import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { TasksState } from '../shared/tasks-state';
import { HouseholdTask } from '../models';

/**
 * "Hjem": husstandens opgaver — engangs-to-dos + gentagne pligter/vedligehold
 * med forfaldsdatoer og valgfri tur-rotation.
 */
@Component({
  selector: 'page-home-tasks',
  imports: [FormsModule],
  template: `
    <h1>🏠 Hjem</h1>
    <p class="muted">Husstandens opgaver og pligter — fælles, ligesom indkøbslisten.</p>

    <!-- Opret -->
    <div class="card">
      <label>Ny opgave eller pligt</label>
      <div class="field">
        <input placeholder="Fx 'Ring til tandlægen' eller 'Støvsug'" [(ngModel)]="newTitle" (keyup.enter)="add()" />
      </div>
      <div class="row-wrap">
        <select [(ngModel)]="newInterval" style="max-width:190px">
          <option [ngValue]="null">Engangsopgave</option>
          <option [ngValue]="1">Gentages: hver dag</option>
          <option [ngValue]="3">Gentages: hver 3. dag</option>
          <option [ngValue]="7">Gentages: hver uge</option>
          <option [ngValue]="14">Gentages: hver 2. uge</option>
          <option [ngValue]="30">Gentages: hver måned</option>
          <option [ngValue]="42">Gentages: hver 6. uge</option>
          <option [ngValue]="90">Gentages: hvert kvartal</option>
          <option [ngValue]="182">Gentages: hvert halve år</option>
          <option [ngValue]="365">Gentages: hvert år</option>
          <option [ngValue]="-1">Andet antal dage…</option>
        </select>
        @if (newInterval === -1) {
          <input type="number" min="1" placeholder="Dage" style="width:80px" [(ngModel)]="customDays" />
        }
        @if (newInterval !== null) {
          <input placeholder="Tur — valgfrit (fx Peter, Clara)" style="max-width:190px" [(ngModel)]="newAssignees" />
        }
        <button class="primary" (click)="add()" [disabled]="saving()">+ Tilføj</button>
      </div>
      @if (error()) { <div class="error">{{ error() }}</div> }
      @if (newInterval !== null) {
        <p class="muted">Pligten står som forfalden i dag — første "Gjort" starter rytmen.
          Tur-feltet er valgfrit: tomt = ingen person på; med navne skiftes I automatisk.</p>
      }
    </div>

    <!-- Forfaldne pligter -->
    @if (overdue().length > 0) {
      <div class="card" style="border-color:var(--danger)">
        <h3>⏰ Forfaldne</h3>
        @for (t of overdue(); track t.id) {
          <div class="list-item">
            <div class="grow">
              <div>{{ t.title }}</div>
              <div class="muted">{{ intervalLabel(t.intervalDays!) }} · forfaldt {{ dueLabel(t) }}
                @if (t.currentAssignee) { · tur: <b>{{ t.currentAssignee }}</b> }
              </div>
            </div>
            <button class="primary small" (click)="complete(t)">✓ Gjort</button>
            <button class="danger" (click)="remove(t)">✕</button>
          </div>
        }
      </div>
    }

    <!-- Engangsopgaver -->
    <div class="card">
      <h3>📋 Opgaver</h3>
      @for (t of openTodos(); track t.id) {
        <label class="list-item" style="cursor:pointer">
          <input type="checkbox" class="check" [checked]="false" (change)="complete(t)" />
          <span class="grow">{{ t.title }}</span>
          <button class="danger" (click)="remove(t); $event.preventDefault()">✕</button>
        </label>
      } @empty { <div class="muted">Ingen åbne opgaver. 🎉</div> }

      @if (doneTodos().length > 0) {
        <div class="muted" style="margin-top:.6rem">Færdige:</div>
        @for (t of doneTodos(); track t.id) {
          <div class="list-item">
            <span class="grow checked">{{ t.title }}</span>
            <button class="btn-link" style="font-size:.75rem" (click)="uncomplete(t)">fortryd</button>
            <button class="danger" (click)="remove(t)">✕</button>
          </div>
        }
      }
    </div>

    <!-- Kommende pligter -->
    <div class="card">
      <h3>🔁 Kommende pligter</h3>
      @for (t of upcoming(); track t.id) {
        <div class="list-item">
          <div class="grow">
            <div>{{ t.title }}</div>
            <div class="muted">{{ intervalLabel(t.intervalDays!) }} · {{ dueLabel(t) }}
              @if (t.currentAssignee) { · tur: <b>{{ t.currentAssignee }}</b> }
            </div>
          </div>
          <button class="small" (click)="complete(t)" title="Gjort før tid">✓</button>
          <button class="danger" (click)="remove(t)">✕</button>
        </div>
      } @empty { <div class="muted">Ingen gentagne pligter endnu. Prøv fx "Støvsug — hver uge".</div> }
    </div>
  `
})
export class HomeTasksPage implements OnInit {
  private api = inject(Api);
  private state = inject(TasksState);

  tasks = signal<HouseholdTask[]>([]);
  error = signal('');
  saving = signal(false);

  newTitle = '';
  newInterval: number | null = null;
  customDays: number | null = null;
  newAssignees = '';

  private todayStr = new Date().toISOString().slice(0, 10);

  overdue = computed(() => this.tasks()
    .filter(t => t.intervalDays != null && t.nextDueDate != null && t.nextDueDate <= this.todayStr));
  upcoming = computed(() => this.tasks()
    .filter(t => t.intervalDays != null && t.nextDueDate != null && t.nextDueDate > this.todayStr));
  openTodos = computed(() => this.tasks().filter(t => t.intervalDays == null && !t.isDone));
  doneTodos = computed(() => this.tasks().filter(t => t.intervalDays == null && t.isDone));

  ngOnInit() { this.load(); }

  load() {
    this.api.getTasks().subscribe(t => this.tasks.set(t));
    this.state.refresh();
  }

  add() {
    const title = this.newTitle.trim();
    if (!title) return;
    const interval = this.newInterval === -1 ? (Number(this.customDays) || null) : this.newInterval;
    if (this.newInterval === -1 && !interval) { this.error.set('Angiv antal dage.'); return; }
    const assignees = interval != null && this.newAssignees.trim()
      ? this.newAssignees.split(',').map(s => s.trim()).filter(s => s)
      : null;

    this.error.set('');
    this.saving.set(true);
    this.api.createTask({ title, intervalDays: interval, assignees }).subscribe({
      next: () => {
        this.saving.set(false);
        this.newTitle = ''; this.newInterval = null; this.customDays = null; this.newAssignees = '';
        this.load();
      },
      error: () => { this.saving.set(false); this.error.set('Kunne ikke oprette.'); }
    });
  }

  complete(t: HouseholdTask) { this.api.completeTask(t.id).subscribe(() => this.load()); }
  uncomplete(t: HouseholdTask) { this.api.uncompleteTask(t.id).subscribe(() => this.load()); }
  remove(t: HouseholdTask) {
    if (!confirm(`Slet "${t.title}"?`)) return;
    this.api.deleteTask(t.id).subscribe(() => this.load());
  }

  // "hver uge", "hver 2. uge", "hver måned", "hvert år", ellers "hver X. dag"
  intervalLabel(days: number): string {
    if (days === 1) return 'hver dag';
    if (days === 7) return 'hver uge';
    if (days % 7 === 0 && days <= 56) return `hver ${days / 7}. uge`;
    if (days === 30 || days === 31) return 'hver måned';
    if (days === 90 || days === 91) return 'hvert kvartal';
    if (days === 182 || days === 183) return 'hvert halve år';
    if (days === 365 || days === 366) return 'hvert år';
    return `hver ${days}. dag`;
  }

  // "i dag", "for 3 dage siden", "om 5 dage (15/7)"
  dueLabel(t: HouseholdTask): string {
    if (!t.nextDueDate) return '';
    const due = new Date(t.nextDueDate + 'T00:00:00');
    const today = new Date(this.todayStr + 'T00:00:00');
    const diff = Math.round((due.getTime() - today.getTime()) / 86400000);
    const dm = `${due.getDate()}/${due.getMonth() + 1}`;
    if (diff === 0) return 'i dag';
    if (diff < 0) return diff === -1 ? 'i går' : `for ${-diff} dage siden`;
    return diff === 1 ? `i morgen (${dm})` : `om ${diff} dage (${dm})`;
  }
}
