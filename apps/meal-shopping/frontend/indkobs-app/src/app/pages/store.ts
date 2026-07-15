import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../api';
import { Store, Order, OrderLine, unitLabel } from '../models';

/**
 * Butiks-demo-side (/butik) — vises til supermarkedet. Ingen husstands-login:
 * adgang med butiks-nøgle. Butikken vælger sig selv, ser indkomne ordrer,
 * pakker linjer og melder klar. Nøgle + valgt butik huskes i localStorage.
 */
@Component({
  selector: 'page-store',
  imports: [FormsModule],
  template: `
    <div class="store-wrap">
      <h1>🏪 Butik — ordrer</h1>

      @if (!authed()) {
        <div class="card">
          <p class="muted">Log ind som butik med den udleverede adgangskode.</p>
          <div class="field">
            <label>Butiks-adgangskode</label>
            <input type="password" [(ngModel)]="key" (keyup.enter)="connect()" placeholder="Adgangskode" />
          </div>
          @if (error()) { <div class="error">{{ error() }}</div> }
          <button class="primary" (click)="connect()" [disabled]="loading()">Log ind</button>
        </div>
      } @else {
        <div class="card">
          <div class="row">
            <select class="grow" [ngModel]="store()" (ngModelChange)="selectStore($event)">
              <option [ngValue]="null">Vælg din butik…</option>
              @for (s of stores(); track s.name) { <option [ngValue]="s.name">{{ s.name }}</option> }
            </select>
            <button (click)="refresh()" title="Opdatér">↻</button>
            <button class="small" (click)="logout()">Log ud</button>
          </div>
        </div>

        @if (store()) {
          @for (o of orders(); track o.id) {
            <div class="card">
              <div class="spread">
                <div class="grow">
                  <h3>{{ o.householdName }}
                    <span class="badge" [class.ready]="o.status === 'Klar'">{{ o.status }}</span>
                  </h3>
                  <div class="muted">{{ packedCount(o) }}/{{ o.lines.length }} pakket</div>
                </div>
                @if (o.status !== 'Klar') {
                  <button class="primary small" (click)="markReady(o)">Marker klar</button>
                } @else {
                  <button class="small" (click)="markCollected(o)">Afhentet</button>
                }
              </div>
              @if (o.note) { <div class="muted" style="margin-top:.3rem">Note: {{ o.note }}</div> }

              @for (line of o.lines; track line.id) {
                <label class="list-item" style="cursor:pointer">
                  <input type="checkbox" class="check" [checked]="line.isPacked" [disabled]="line.notAvailable"
                         (change)="setPacked(o, line, $event)" />
                  <span class="grow" [class.checked]="line.isPacked || line.notAvailable">
                    {{ line.name }} — {{ qty(line.quantity) }} {{ label(line.unit) }}
                    @if (line.categoryName) { <span class="muted" style="font-size:.72rem">· {{ line.categoryName }}</span> }
                    @if (line.notAvailable) { <span class="badge">ikke på lager</span> }
                  </span>
                  <button class="btn-link" style="font-size:.72rem"
                          (click)="toggleAvail(o, line); $event.preventDefault()">
                    {{ line.notAvailable ? 'på lager igen' : 'mangler' }}
                  </button>
                </label>
              }
            </div>
          } @empty {
            <div class="empty">Ingen aktive ordrer for {{ store() }} lige nu.</div>
          }
        }
      }
    </div>
  `,
  styles: [`.store-wrap { max-width: 720px; margin: 0 auto; }`]
})
export class StorePage implements OnInit {
  private api = inject(Api);

  key = '';
  authed = signal(false);
  stores = signal<Store[]>([]);
  store = signal<string | null>(null);
  orders = signal<Order[]>([]);
  error = signal('');
  loading = signal(false);

  label = unitLabel;

  ngOnInit() {
    const savedKey = localStorage.getItem('indkobs.storeKey');
    if (savedKey) { this.key = savedKey; this.connect(); }
  }

  connect() {
    if (!this.key.trim()) return;
    this.loading.set(true);
    this.error.set('');
    this.api.storeGetStores(this.key).subscribe({
      next: s => {
        this.loading.set(false);
        this.authed.set(true);
        this.stores.set(s);
        localStorage.setItem('indkobs.storeKey', this.key);
        const savedStore = localStorage.getItem('indkobs.storeName');
        if (savedStore && s.some(x => x.name === savedStore)) this.selectStore(savedStore);
      },
      error: () => { this.loading.set(false); this.error.set('Forkert adgangskode.'); }
    });
  }

  selectStore(name: string | null) {
    this.store.set(name);
    if (name) { localStorage.setItem('indkobs.storeName', name); this.refresh(); }
    else this.orders.set([]);
  }

  refresh() {
    const s = this.store();
    if (!s) return;
    this.api.storeGetOrders(this.key, s).subscribe(o => this.orders.set(o));
  }

  private replace(updated: Order) {
    this.orders.update(list => list.map(o => o.id === updated.id ? updated : o));
  }

  setPacked(o: Order, line: OrderLine, ev: Event) {
    const packed = (ev.target as HTMLInputElement).checked;
    this.api.storePackLine(this.key, o.id, line.id, { isPacked: packed, notAvailable: line.notAvailable })
      .subscribe(u => this.replace(u));
  }
  toggleAvail(o: Order, line: OrderLine) {
    const na = !line.notAvailable;
    this.api.storePackLine(this.key, o.id, line.id, { isPacked: na ? false : line.isPacked, notAvailable: na })
      .subscribe(u => this.replace(u));
  }
  markReady(o: Order) { this.api.storeReady(this.key, o.id).subscribe(u => this.replace(u)); }
  markCollected(o: Order) { this.api.storeCollected(this.key, o.id).subscribe(() => this.refresh()); }

  logout() {
    localStorage.removeItem('indkobs.storeKey');
    localStorage.removeItem('indkobs.storeName');
    this.authed.set(false); this.store.set(null); this.orders.set([]); this.key = '';
  }

  packedCount(o: Order) { return o.lines.filter(l => l.isPacked || l.notAvailable).length; }
  qty(n: number) { return n.toLocaleString('da-DK', { maximumFractionDigits: 3 }); }
}
