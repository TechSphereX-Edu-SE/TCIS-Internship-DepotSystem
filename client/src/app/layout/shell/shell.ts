import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth-service';
import { ToastHost } from '../../shared/toast/toast-host';

interface MenuSection {
  title: string;
  items: { label: string; path: string }[];
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHost],
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly auth = inject(AuthService);

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
}
