import { Routes } from '@angular/router';
import { Layout } from './app/layout/layout';
import { Home } from './app/pages/home/home';

export const routes: Routes = [
  {
    path: '',
    component: Layout,
    children: [
      // Trang chủ render trong <router-outlet /> của Layout
      { path: '', component: Home, data: { title: 'Trang chủ' } },
      {
        path: 'cau-hinh',
        loadChildren: () => import('./app/pages/config/config.router').then((m) => m.configRoutes)
      },
      {
        path: 'dong-goi',
        loadChildren: () => import('./app/pages/dong-goi/dong-goi.router').then((m) => m.dongGoiRoutes)
      },
    ]
  }
];
