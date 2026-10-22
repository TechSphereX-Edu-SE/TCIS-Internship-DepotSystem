import { Component, inject } from '@angular/core';
import { ToastService } from './toast-service';

// Renders the toasts of ToastService; placed once in the app shell
@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toastService.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.tone === 'error'" role="status">
          <span class="dot" aria-hidden="true"></span>
          <span class="message">{{ toast.message }}</span>
          <button type="button" class="close" aria-label="Đóng" (click)="toastService.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      top: 24px;
      right: 32px;
      z-index: 100;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .toast {
      display: flex;
      align-items: center;
      gap: 12px;
      max-width: 440px;
      padding: 12px 14px 12px 16px;
      border-radius: 8px;
      background: var(--ink);
      color: #fff;
      font-size: 14px;
      font-weight: 500;
      box-shadow: 0 8px 24px rgb(23 33 43 / 22%);
    }
    .dot {
      width: 10px;
      height: 10px;
      border-radius: 5px;
      background: #22c55e;
      flex: none;
    }
    .toast.error .dot {
      background: #ef4444;
    }
    .message {
      flex: 1;
    }
    .close {
      border: 0;
      background: transparent;
      color: #8b98a5;
      font-size: 18px;
      cursor: pointer;
    }
        /* Narrow screens: full width with a 16px margin, below the 56px top bar of the shell */
    @media (max-width: 899.98px) {
      .toasts {
        top: 64px;
        left: 16px;
        right: 16px;
      }
      .toast {
        max-width: none;
      }
    }
  `,
})
export class ToastHost {
  protected readonly toastService = inject(ToastService);
}
