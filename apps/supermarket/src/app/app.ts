import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from './api';
import { Store, Order, OrderLine, unitLabel } from './models';

/**
 * Butiks-app (selvstændigt website). Adgang med butiks-nøgle (ikke husstands-login).
 * Butikken vælger sig selv, ser indkomne ordrer, pakker linjer og melder klar.
 * Taler med den fælles backend via /api/store/*.
 */
@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
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
    const savedKey = localStorage.getItem('butik.key');
    if (savedKey) { this.key = savedKey; this.connect(); }
  }

  connect() {
    if (!this.key.trim()) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getStores(this.key).subscribe({
      next: s => {
        this.loading.set(false);
        this.authed.set(true);
        this.stores.set(s);
        localStorage.setItem('butik.key', this.key);
        const saved = localStorage.getItem('butik.store');
        if (saved && s.some(x => x.name === saved)) this.selectStore(saved);
      },
      error: () => { this.loading.set(false); this.error.set('Forkert adgangskode.'); }
    });
  }

  selectStore(name: string | null) {
    this.store.set(name);
    if (name) { localStorage.setItem('butik.store', name); this.refresh(); }
    else this.orders.set([]);
  }

  refresh() {
    const s = this.store();
    if (!s) return;
    this.api.getOrders(this.key, s).subscribe(o => this.orders.set(o));
  }

  private replace(u: Order) { this.orders.update(l => l.map(o => o.id === u.id ? u : o)); }

  setPacked(o: Order, line: OrderLine, ev: Event) {
    const packed = (ev.target as HTMLInputElement).checked;
    this.api.packLine(this.key, o.id, line.id, { isPacked: packed, notAvailable: line.notAvailable })
      .subscribe(u => this.replace(u));
  }
  toggleAvail(o: Order, line: OrderLine) {
    const na = !line.notAvailable;
    this.api.packLine(this.key, o.id, line.id, { isPacked: na ? false : line.isPacked, notAvailable: na })
      .subscribe(u => this.replace(u));
  }
  markReady(o: Order) { this.api.ready(this.key, o.id).subscribe(u => this.replace(u)); }
  markCollected(o: Order) { this.api.collected(this.key, o.id).subscribe(() => this.refresh()); }

  logout() {
    localStorage.removeItem('butik.key');
    localStorage.removeItem('butik.store');
    this.authed.set(false); this.store.set(null); this.orders.set([]); this.key = '';
  }

  packedCount(o: Order) { return o.lines.filter(l => l.isPacked || l.notAvailable).length; }
  qty(n: number) { return n.toLocaleString('da-DK', { maximumFractionDigits: 3 }); }
}
