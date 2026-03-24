import { Component, inject } from '@angular/core';
import { BreadcrumbService } from '../../services/breadcrumb.service';

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  template: `
    @if (bc.crumbs().length > 0) {
      <nav class="zbreadcrumb" aria-label="Breadcrumb">
        <span class="zbreadcrumb-item" style="opacity:.5">🏠 Home</span>
        @for (crumb of bc.crumbs(); track $index; let last = $last) {
          <span class="zbreadcrumb-sep">›</span>
          @if (last) {
            <span class="zbreadcrumb-current">{{ crumb.label }}</span>
          } @else {
            <span class="zbreadcrumb-item">{{ crumb.label }}</span>
          }
        }
      </nav>
    }
  `
})
export class BreadcrumbComponent {
  readonly bc = inject(BreadcrumbService);
}
