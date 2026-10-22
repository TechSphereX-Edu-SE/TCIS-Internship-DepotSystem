import { Service, signal } from '@angular/core';

export interface Toast {
  id: number;
  tone: 'success' | 'error';
  message: string;
}

// Short confirmation messages in the top-right corner (Toast component in the mockups)
@Service()
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private nextId = 1;

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(tone: Toast['tone'], message: string): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { id, tone, message }]);
    // Disappears by itself; the user can also close it earlier
    setTimeout(() => this.dismiss(id), 5000);
  }
}
