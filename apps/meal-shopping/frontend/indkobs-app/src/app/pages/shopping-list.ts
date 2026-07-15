import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api } from '../api';
import { WeekState } from '../shared/week-state';
import { ToastService } from '../shared/toast';
import { ShoppingList, ShoppingLine, Ingredient, Unit, UNITS, unitLabel, Store, Order } from '../models';

@Component({
  selector: 'page-shopping-list',
  imports: [FormsModule, RouterLink],
  template: `
    <h1>Indkøbsliste</h1>

    @if (list(); as l) {
      <div class="spread">
        <div class="muted">Uge {{ l.weekNumber }}, {{ l.year }}</div>
        <div class="row">
          <button class="small" (click)="share()">🔗 Del liste</button>
          <div class="badge">{{ checkedCount() }} / {{ totalCount() }} købt</div>
        </div>
      </div>

      @if (shareUrl()) {
        <div class="card" style="border-color:var(--primary)">
          <div class="muted">Delings-link ({{ shareCopied() ? 'kopieret ✅' : 'send til den der handler' }}):</div>
          <div style="word-break:break-all; font-size:.85rem; margin:.3rem 0">{{ shareUrl() }}</div>
          <div class="row">
            <button class="small" (click)="copyShare()">Kopiér</button>
            <button class="small danger" (click)="revokeShare()">Stop deling</button>
          </div>
        </div>
      }

      <!-- To kolonner på desktop: liste til venstre, handlinger (send/ordrer) til højre.
           På mobil stables handlingerne ØVERST (grid-template-areas skifter). -->
      <div class="sl-grid">
        <aside class="sl-side">
          <!-- Send til butik -->
          <div class="card">
            <h3>Send til butik</h3>
            <p class="muted">Butikken pakker den og melder klar til afhentning.</p>
            <div class="field">
              <select [(ngModel)]="orderStore">
                <option [ngValue]="null">Vælg butik…</option>
                @for (s of stores(); track s.name) { <option [ngValue]="s.name">{{ s.name }}</option> }
              </select>
            </div>
            <button class="primary" style="width:100%" (click)="sendOrder()" [disabled]="!orderStore || sending()">
              {{ sending() ? 'Sender…' : 'Send til butik' }}
            </button>
            @if (orderMsg()) { <div class="muted" style="margin-top:.4rem">{{ orderMsg() }}</div> }
          </div>

          <!-- Mine ordrer + status (opdateres automatisk) -->
          @if (myOrders().length > 0) {
            <div class="card">
              <h3>Mine ordrer</h3>
              @for (o of myOrders(); track o.id) {
                <div class="list-item">
                  <div class="grow">
                    <div>{{ o.storeName }}
                      <span class="badge" [class.ready]="o.status === 'Klar'">{{ statusLabel(o.status) }}</span>
                    </div>
                    <div class="muted">{{ o.lines.length }} varer · sendt {{ shortDate(o.createdUtc) }}</div>
                  </div>
                  <button class="danger" (click)="cancelOrder(o)" title="Fjern">✕</button>
                </div>
              }
              <p class="muted" style="font-size:.72rem; margin-top:.4rem">Status opdateres automatisk.</p>
            </div>
          }
        </aside>

        <div class="sl-main">
          @if (checkedCount() > 0) {
            <div class="card">
              <div class="spread">
                <div class="grow">
                  <b>Færdig med at handle?</b>
                  <div class="muted">Læg de {{ checkedCount() }} afkrydsede varer på køkkenlageret med ét tryk.</div>
                </div>
                <button class="primary small" (click)="stock()" [disabled]="stocking()">
                  {{ stocking() ? '…' : '🥫 Læg på lager' }}
                </button>
              </div>
              @if (stockMsg()) { <div class="muted" style="margin-top:.4rem">{{ stockMsg() }}</div> }
            </div>
          }

          @for (g of l.groups; track g.categoryName) {
            <div class="card">
              <h3>{{ g.categoryName }}</h3>
              @for (line of g.lines; track line.lineKey) {
                <label class="list-item" style="cursor:pointer">
                  <input type="checkbox" class="check" [checked]="line.isChecked"
                         (change)="toggle(line)" />
                  <span class="grow" [class.checked]="line.isChecked || line.quantity === 0">
                    {{ line.name }} — {{ qty(line.quantity) }} {{ label(line.unit) }}
                    @if (line.isManual) { <span class="badge">løs</span> }
                    @if (line.quantity === 0) { <span class="badge">dækket af lager</span> }
                    @else if (line.onHandQuantity != null) {
                      <span class="badge">har {{ qty(line.onHandQuantity) }} {{ label(line.onHandUnit!) }} hjemme</span>
                    }
                  </span>
                  <span class="muted" style="font-size:.72rem">{{ line.sources.join(', ') }}</span>
                </label>
              }
            </div>
          } @empty {
            <div class="empty">Listen er tom. Tilføj retter eller varegrupper på Uge-fanen.</div>
          }

          <!-- Hurtig tilføjelse af løs vare -->
          <div class="card">
            <h3>Tilføj løs vare</h3>
            <div class="line-input">
              <input class="full" list="sl-ing" placeholder="Vare" [(ngModel)]="text" />
              <input type="number" min="0" step="0.001" placeholder="Antal" [(ngModel)]="qtyInput" />
              <select [(ngModel)]="unit">
                @for (u of units; track u.value) { <option [value]="u.value">{{ u.label }}</option> }
              </select>
              <button class="primary" (click)="add()">+</button>
            </div>
            <datalist id="sl-ing">
              @for (i of ingredients(); track i.id) { <option [value]="i.name"></option> }
            </datalist>
            <p class="muted">Løse varer kan fjernes igen på <a routerLink="/uge">Uge-fanen</a>.</p>
          </div>
        </div>
      </div>
    } @else {
      <div class="empty">
        Ingen uge valgt.<br />
        <a routerLink="/uge">Gå til Ugeplan og vælg en uge.</a>
      </div>
    }
  `
})
export class ShoppingListPage implements OnInit, OnDestroy {
  private api = inject(Api);
  private state = inject(WeekState);
  private toast = inject(ToastService);
  private pollId?: ReturnType<typeof setInterval>;

  list = signal<ShoppingList | null>(null);
  ingredients = signal<Ingredient[]>([]);

  totalCount = computed(() => this.list()?.groups.reduce((s, g) => s + g.lines.length, 0) ?? 0);
  checkedCount = computed(() =>
    this.list()?.groups.reduce((s, g) => s + g.lines.filter(l => l.isChecked).length, 0) ?? 0);

  units = UNITS;
  label = unitLabel;
  text = '';
  qtyInput = 1;
  unit: Unit = 'Stk';
  shareUrl = signal('');
  shareCopied = signal(false);
  stocking = signal(false);
  stockMsg = signal('');

  // Ordrer (send til butik)
  stores = signal<Store[]>([]);
  myOrders = signal<Order[]>([]);
  orderStore: string | null = null;
  sending = signal(false);
  orderMsg = signal('');

  ngOnInit() {
    this.api.getIngredients().subscribe(i => this.ingredients.set(i));
    this.api.getStores().subscribe(s => this.stores.set(s));
    this.loadOrders();
    this.load();
    // Auto-opdatér ordre-status hvert 15. sek, så "Klar" dukker op uden genindlæsning.
    this.pollId = setInterval(() => this.loadOrders(), 15000);
  }

  ngOnDestroy() { if (this.pollId) clearInterval(this.pollId); }

  loadOrders() { this.api.getMyOrders().subscribe(o => this.myOrders.set(o)); }

  sendOrder() {
    const id = this.state.selectedWeekId();
    if (!id || !this.orderStore) return;
    this.sending.set(true);
    this.orderMsg.set('');
    this.api.createOrderFromWeek(id, { storeName: this.orderStore }).subscribe({
      next: () => {
        this.sending.set(false);
        this.orderMsg.set(`✅ Sendt til ${this.orderStore}. Følg status under "Mine ordrer".`);
        this.orderStore = null;
        this.loadOrders();
      },
      error: (e) => {
        this.sending.set(false);
        this.orderMsg.set(e.status === 400 ? 'Listen er tom — intet at sende.' : 'Kunne ikke sende ordren.');
      }
    });
  }

  cancelOrder(o: Order) {
    if (!confirm(`Fjern ordren til ${o.storeName}?`)) return;
    this.api.cancelOrder(o.id).subscribe(() => this.loadOrders());
  }

  statusLabel(s: string): string {
    switch (s) {
      case 'Modtaget': return '🕐 Modtaget';
      case 'Pakkes': return '📦 Pakkes';
      case 'Klar': return '✅ Klar til afhentning';
      case 'Afhentet': return 'Afhentet';
      default: return s;
    }
  }
  shortDate(iso: string): string {
    const d = new Date(iso);
    return `${d.getDate()}/${d.getMonth() + 1} ${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`;
  }

  load() {
    const id = this.state.selectedWeekId();
    if (!id) { this.list.set(null); return; }
    this.api.getShoppingList(id).subscribe({
      next: l => this.list.set(l),
      error: () => this.list.set(null)
    });
  }

  // Optimistisk afkrydsning: opdater UI med det samme, gem i baggrunden.
  toggle(line: ShoppingLine) {
    const id = this.state.selectedWeekId();
    if (!id) return;
    const next = !line.isChecked;
    line.isChecked = next;
    this.list.update(l => l ? { ...l } : l); // trig change detection
    this.api.setCheck(id, line.lineKey, next).subscribe({ error: () => { line.isChecked = !next; } });
  }

  add() {
    const id = this.state.selectedWeekId();
    if (!id || !this.text.trim()) return;
    this.api.addWeekManualItem(id, { freeText: this.text.trim(), quantity: Number(this.qtyInput) || 1, unit: this.unit })
      .subscribe(() => { this.text = ''; this.qtyInput = 1; this.load(); });
  }

  // Læg alle afkrydsede (købte) varer på køkkenlageret. Naturligt idempotent:
  // efter første tryk dækker lageret varerne, så endnu et tryk tilføjer intet.
  stock() {
    const id = this.state.selectedWeekId();
    if (!id) return;
    if (!confirm('Læg alle afkrydsede varer på køkkenlageret?')) return;
    this.stocking.set(true);
    this.api.stockChecked(id).subscribe({
      next: r => {
        this.stocking.set(false);
        this.stockMsg.set(r.linesStocked > 0
          ? `✅ ${r.linesStocked} varer lagt på lageret. Listen viser nu at de er dækket.`
          : 'Intet at lægge på lager (varerne er allerede dækket).');
        this.load(); // genindlæs: linjerne bliver "dækket af lager"
      },
      error: () => { this.stocking.set(false); this.stockMsg.set('Kunne ikke lægge på lager.'); }
    });
  }

  // Opret delings-link (uden login) og vis det, så det kan sendes til den der handler.
  share() {
    const id = this.state.selectedWeekId();
    if (!id) return;
    this.api.createShare(id).subscribe(r => {
      this.shareUrl.set(`${location.origin}/del/${r.token}`);
      this.shareCopied.set(false);
      this.copyShare();
    });
  }

  copyShare() {
    const url = this.shareUrl();
    if (!url) return;
    navigator.clipboard?.writeText(url).then(
      () => { this.shareCopied.set(true); this.toast.success('Delings-link kopieret'); },
      () => this.shareCopied.set(false));
  }

  revokeShare() {
    const id = this.state.selectedWeekId();
    if (!id || !confirm('Stop delingen? Linket holder op med at virke.')) return;
    this.api.revokeShare(id).subscribe(() => { this.shareUrl.set(''); this.shareCopied.set(false); });
  }

  qty(n: number): string {
    // Dansk format: komma som decimaltegn, ingen overflødige nuller.
    return n.toLocaleString('da-DK', { maximumFractionDigits: 3 });
  }
}
