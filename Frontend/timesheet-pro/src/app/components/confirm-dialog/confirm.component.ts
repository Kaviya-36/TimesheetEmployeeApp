import { Component, Input, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-confirm',
  standalone: true,
  template: `
    @if (visible) {
      <div class="zmodal-overlay" (click)="onCancel()">
        <div class="zconfirm-box" (click)="$event.stopPropagation()">
          <span class="zconfirm-icon">
            @if (type === 'danger')  { 🗑️ }
            @if (type === 'warning') { ⚠️ }
            @if (type === 'info')    { ℹ️ }
          </span>
          <h3 class="zconfirm-title">{{ title }}</h3>
          <p class="zconfirm-msg">{{ message }}</p>
          <div class="zconfirm-actions">
            <button class="zbtn zbtn-outline" (click)="onCancel()">{{ cancelLabel }}</button>
            <button class="zbtn"
              [class.zbtn-danger]="type === 'danger'"
              [class.zbtn-warning]="type === 'warning'"
              [class.zbtn-primary]="type === 'info'"
              (click)="onConfirm()">{{ confirmLabel }}</button>
          </div>
        </div>
      </div>
    }
  `
})
export class ConfirmComponent {
  @Input() visible      = false;
  @Input() title        = 'Confirm Action';
  @Input() message      = 'Are you sure you want to proceed? This action cannot be undone.';
  @Input() confirmLabel = 'Confirm';
  @Input() cancelLabel  = 'Cancel';
  @Input() type: 'danger' | 'warning' | 'info' = 'danger';

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  onConfirm() { this.confirmed.emit(); }
  onCancel()  { this.cancelled.emit(); }
}
