import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth-service';
import { ToastHost } from '../../shared/toast/toast-host';

interface MenuSection {
  title: string;
  items: { label: string; path: string }[];
}

/*
  App layout: sidebar menu + page content.
  Wide screens (>= 900px): the sidebar is always visible on the left (mockups).
  Narrow screens (phones, small tablets): a top bar with a menu button; the sidebar slides in
  over the page as a drawer and closes after navigating, on the backdrop or with Escape.
  The drawer state only matters on narrow screens; the CSS ignores it on wide screens.
*/
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHost],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
  host: { '(document:keydown.escape)': 'closeMenu()' },
})
export class Shell {
  protected readonly auth = inject(AuthService);

  // Drawer open on narrow screens
  protected readonly menuOpen = signal(false);

  private readonly menuButton = viewChild<ElementRef<HTMLButtonElement>>('menuButton');
  private readonly firstLink = viewChild<ElementRef<HTMLAnchorElement>>('firstLink');

  // Same three groups as the sidebar in the mockups
  protected readonly menu: MenuSection[] = [
    {
      title: 'Nghiệp vụ',
      items: [
        { label: 'Nhập bãi', path: '/gate-in' },
        { label: 'Vị trí & đảo chuyển', path: '/yard' },
        { label: 'Xuất bãi', path: '/gate-out' },
        { label: 'Lệnh giao', path: '/delivery-orders' },
      ],
    },
    {
      title: 'Thống kê',
      items: [
        { label: 'Tồn bãi', path: '/statistics/inventory' },
        { label: 'Sản lượng nhập/xuất', path: '/statistics/throughput' },
      ],
    },
    {
      title: 'Danh mục',
      items: [
        { label: 'Container', path: '/catalog/containers' },
        { label: 'Hãng khai thác', path: '/catalog/line-operators' },
        { label: 'Khách hàng', path: '/catalog/customers' },
        { label: 'Block', path: '/catalog/blocks' },
      ],
    },
  ];

  constructor() {
    // Picking a page in the drawer closes it, so the page is visible right away
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));
  }

  protected openMenu(): void {
    this.menuOpen.set(true);
    // Keyboard users land inside the drawer; wait one frame for it to become visible
    requestAnimationFrame(() => this.firstLink()?.nativeElement.focus());
  }

  // Also bound to Escape; does nothing when the drawer is already closed
  protected closeMenu(): void {
    if (!this.menuOpen()) return;
    this.menuOpen.set(false);
    // Give focus back to the button that opened the drawer
    this.menuButton()?.nativeElement.focus();
  }
}
