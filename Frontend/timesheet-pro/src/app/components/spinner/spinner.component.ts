import { Component, inject } from '@angular/core';
import { LoadingService } from '../../services/loading.service';

@Component({
  selector: 'app-spinner',
  standalone: true,
  template: `
    @if (loading.loading()) {
      <div class="zspinner-overlay" role="status" aria-label="Loading">
        <div class="zspinner-ring">
          <div></div><div></div><div></div><div></div>
        </div>
      </div>
    }
  `,
  styles: [`
    .zspinner-overlay {
      position: fixed; inset: 0;
      background: rgba(255,255,255,.6);
      backdrop-filter: blur(2px);
      display: flex; align-items: center; justify-content: center;
      z-index: 9998;
    }
    .zspinner-ring {
      width: 48px; height: 48px; position: relative;
    }
    .zspinner-ring div {
      box-sizing: border-box; display: block; position: absolute;
      width: 40px; height: 40px; margin: 4px;
      border: 4px solid transparent;
      border-top-color: #E05B2B;
      border-radius: 50%;
      animation: spin .8s cubic-bezier(.5,0,.5,1) infinite;
    }
    .zspinner-ring div:nth-child(1) { animation-delay: -.24s; }
    .zspinner-ring div:nth-child(2) { animation-delay: -.16s; }
    .zspinner-ring div:nth-child(3) { animation-delay: -.08s; }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class SpinnerComponent {
  readonly loading = inject(LoadingService);
}
