import { Injectable, signal } from '@angular/core';

export interface BreadcrumbItem {
  label: string;
  tab?:  string;
}

@Injectable({ providedIn: 'root' })
export class BreadcrumbService {
  private _crumbs = signal<BreadcrumbItem[]>([]);
  readonly crumbs = this._crumbs.asReadonly();

  set(items: BreadcrumbItem[]) { this._crumbs.set(items); }
  push(item: BreadcrumbItem)   { this._crumbs.update(c => [...c, item]); }
  clear()                       { this._crumbs.set([]); }
}
