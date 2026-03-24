import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private _n = 0;
  private _loading = signal(false);
  readonly loading = this._loading.asReadonly();

  show() { this._n++; this._loading.set(true); }
  hide() { this._n = Math.max(0, this._n - 1); if (this._n === 0) this._loading.set(false); }
}
