import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth-guard';
import { Shell } from './layout/shell/shell';

// Placeholder route for screens not built yet; replaced one by one in the next steps
const soon = (path: string, heading: string, group: string) => ({
  path,
  title: `${heading} · Depot`,
  loadComponent: () => import('./features/coming-soon/coming-soon').then((m) => m.ComingSoon),
  data: { heading, group },
});

export const routes: Routes = [
  {
    path: 'login',
    title: 'Đăng nhập · Depot',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login-page/login-page').then((m) => m.LoginPage),
  },
  {
    // Every page inside the sidebar layout requires a valid token
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [

      { path: '', pathMatch: 'full', redirectTo: 'gate-in' },
      {
        path: 'gate-in',
        title: 'Nhập bãi · Depot',
        loadComponent: () => import('./features/gate-in/gate-in-page').then((m) => m.GateInPage),
      },
      {
        path: 'yard',
        title: 'Vị trí & đảo chuyển · Depot',
        loadComponent: () => import('./features/yard/yard-page').then((m) => m.YardPage),
      },
      soon('gate-out', 'Xuất bãi (Gate-out)', 'Nghiệp vụ · Nhập/xuất container'),
      soon('delivery-orders', 'Lệnh giao container rỗng', 'Nghiệp vụ · Lệnh của hãng khai thác'),
      soon('statistics/inventory', 'Container tồn bãi theo hãng', 'Thống kê'),
      soon('statistics/throughput', 'Sản lượng nhập/xuất', 'Thống kê'),
      soon('catalog/containers', 'Container', 'Danh mục'),
      soon('catalog/line-operators', 'Hãng khai thác', 'Danh mục'),
      soon('catalog/customers', 'Khách hàng', 'Danh mục'),
      soon('catalog/blocks', 'Block', 'Danh mục'),
    ],
  },
  { path: '**', redirectTo: '' },
];
