import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth-guard';
import { Shell } from './layout/shell/shell';

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
      {
        path: 'catalog/containers',
        title: 'Container · Depot',
        loadComponent: () => import('./features/catalog/containers-page').then((m) => m.ContainersPage),
      },
      {
        path: 'catalog/line-operators',
        title: 'Hãng khai thác · Depot',
        loadComponent: () => import('./features/catalog/line-operators-page').then((m) => m.LineOperatorsPage),
      },
      {
        path: 'catalog/customers',
        title: 'Khách hàng · Depot',
        loadComponent: () => import('./features/catalog/customers-page').then((m) => m.CustomersPage),
      },
      {
        path: 'catalog/blocks',
        title: 'Block · Depot',
        loadComponent: () => import('./features/catalog/blocks-page').then((m) => m.BlocksPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
