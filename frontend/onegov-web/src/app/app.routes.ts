import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
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
        path: 'sign-in',
        loadComponent: () => import('./features/auth/sign-in.page').then((m) => m.SignInPage),
      },
      {
        path: 'register',
        loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
      },
      {
        path: 'auth/national-id/mock',
        loadComponent: () =>
          import('./features/auth/national-id-mock.page').then((m) => m.NationalIdMockPage),
      },
      {
        path: 'auth/national-id/callback',
        loadComponent: () =>
          import('./features/auth/national-id-callback.page').then((m) => m.NationalIdCallbackPage),
      },
      {
        path: 'applications',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/applications/applications-list.page').then((m) => m.ApplicationsListPage),
      },
      {
        path: 'applications/:id',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/applications/application-detail.page').then((m) => m.ApplicationDetailPage),
      },
      {
        path: 'account',
        canActivate: [authGuard],
        loadComponent: () => import('./features/auth/account.page').then((m) => m.AccountPage),
      },
      {
        path: '**',
        loadComponent: () => import('./features/not-found.page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
