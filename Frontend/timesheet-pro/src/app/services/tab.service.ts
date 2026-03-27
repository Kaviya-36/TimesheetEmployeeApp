import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TabService {
  readonly activeTab = signal<string>('');
  setTab(tab: string) { this.activeTab.set(tab); }
}
