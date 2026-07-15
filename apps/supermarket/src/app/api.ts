import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Store, Order } from './models';
import { environment } from '../environments/environment';

// Fælles backend (samme API som forbruger-appen). Lokalt: samme vært, port 5298.
const API = environment.apiBase || `${location.protocol}//${location.hostname}:5298/api`;

/** Kald mod butiks-endpointsene. Adgang via butiks-nøgle i header X-Store-Key. */
@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  private h(key: string) { return { headers: { 'X-Store-Key': key } }; }

  getStores(key: string): Observable<Store[]> {
    return this.http.get<Store[]>(`${API}/store/stores`, this.h(key));
  }
  getOrders(key: string, store: string): Observable<Order[]> {
    return this.http.get<Order[]>(`${API}/store/orders`, { headers: { 'X-Store-Key': key }, params: { store } });
  }
  packLine(key: string, orderId: number, lineId: number, body: { isPacked: boolean; notAvailable: boolean }) {
    return this.http.put<Order>(`${API}/store/orders/${orderId}/lines/${lineId}`, body, this.h(key));
  }
  ready(key: string, orderId: number) { return this.http.post<Order>(`${API}/store/orders/${orderId}/ready`, {}, this.h(key)); }
  collected(key: string, orderId: number) { return this.http.post<Order>(`${API}/store/orders/${orderId}/collected`, {}, this.h(key)); }
}
