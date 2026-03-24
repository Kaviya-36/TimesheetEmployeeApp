import { Component, inject } from '@angular/core';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  template: `
    <div class="ztoast-container">
      @for (t of toast.toasts(); track t.id) {
        <div class="ztoast ztoast-{{ t.type }}" role="alert">
          <span class="ztoast-icon">
            @if (t.type === 'success') { ✅ }
            @if (t.type === 'error')   { ❌ }
            @if (t.type === 'warning') { ⚠️ }
            @if (t.type === 'info')    { ℹ️ }
          </span>
          <div class="ztoast-body">
            <div class="ztoast-title">{{ t.title }}</div>
            @if (t.message) { <div class="ztoast-msg">{{ t.message }}</div> }
          </div>
          <button class="ztoast-close" (click)="toast.remove(t.id)">×</button>
        </div>
      }
    </div>
  `
})
export class ToastComponent {
  readonly toast = inject(ToastService);
}
