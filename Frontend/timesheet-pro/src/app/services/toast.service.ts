import { Injectable, signal } from '@angular/core';

export interface Toast {
  id:      number;
  title:   string;
  message: string;
  type:   'success' | 'error' | 'warning' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private _toasts = signal<Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();
  private next = 0;

  success(title: string, message = '') { this.add(title, message, 'success'); }
  error  (title: string, message = '') { this.add(title, message, 'error');   }
  warning(title: string, message = '') { this.add(title, message, 'warning'); }
  info   (title: string, message = '') { this.add(title, message, 'info');    }

  private add(title: string, message: string, type: Toast['type']) {
    const id = ++this.next;
    this._toasts.update(t => [...t, { id, title, message, type }]);
    setTimeout(() => this.remove(id), 4500);
  }

  remove(id: number) {
    this._toasts.update(t => t.filter(x => x.id !== id));
  }
}
