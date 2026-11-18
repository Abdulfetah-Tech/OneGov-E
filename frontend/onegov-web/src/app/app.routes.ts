import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage) },
      {
        path: 'services',
        loadComponent: () =>
          import('./features/services/service-directory.page').then((m) => m.ServiceDirectoryPage),
      },
      {
        path: 'services/:code',
        loadComponent: () =>
          import('./features/services/service-detail.page').then((m) => m.ServiceDetailPage),
      },
      {
        path: '**',
        loadComponent: () => import('./features/not-found.page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
