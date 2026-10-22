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
      {
        path: 'gate-out',
        title: 'Xuất bãi · Depot',
        loadComponent: () => import('./features/gate-out/gate-out-page').then((m) => m.GateOutPage),
      },
            {
        path: 'delivery-orders',
        title: 'Lệnh giao · Depot',
        loadComponent: () =>
          import('./features/delivery-orders/delivery-orders-page').then((m) => m.DeliveryOrdersPage),
      },
      {
        path: 'statistics/inventory',
        title: 'Tồn bãi · Depot',
        loadComponent: () => import('./features/statistics/inventory-page').then((m) => m.InventoryPage),
      },
      {
        path: 'statistics/throughput',
        title: 'Sản lượng nhập/xuất · Depot',
        loadComponent: () => import('./features/statistics/throughput-page').then((m) => m.ThroughputPage),
      },
      soon('catalog/containers', 'Container', 'Danh mục'),
      soon('catalog/line-operators', 'Hãng khai thác', 'Danh mục'),
      soon('catalog/customers', 'Khách hàng', 'Danh mục'),
      soon('catalog/blocks', 'Block', 'Danh mục'),
    ],
  },
  { path: '**', redirectTo: '' },
];
