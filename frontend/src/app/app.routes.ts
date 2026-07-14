import { Routes } from '@angular/router';
import { authGuard, guestGuard, platformGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    // Public marketing page — what future customers see first
    path: 'welcome',
    loadComponent: () => import('./features/landing/landing').then((m) => m.Landing),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register').then((m) => m.Register),
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    // NOT guest-guarded: invited staff may open this while an admin session
    // exists on the same machine
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password').then((m) => m.ResetPassword),
  },
  {
    // Staff invitations: "Join {clinic}" — also not guest-guarded
    path: 'accept-invite',
    loadComponent: () =>
      import('./features/auth/accept-invite').then((m) => m.AcceptInvite),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'patients',
        loadComponent: () => import('./features/patients/patients').then((m) => m.Patients),
      },
      {
        path: 'appointments',
        loadComponent: () =>
          import('./features/appointments/appointments').then((m) => m.Appointments),
      },
      {
        path: 'inventory',
        loadComponent: () => import('./features/inventory/inventory').then((m) => m.Inventory),
      },
      {
        // "My clinics" — structure view of every membership + open new
        path: 'clinics',
        loadComponent: () => import('./features/clinics/clinics').then((m) => m.Clinics),
      },
      {
        path: 'reports',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/reports/reports').then((m) => m.Reports),
      },
      {
        path: 'staff',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/staff/staff').then((m) => m.Staff),
      },
      {
        path: 'billing',
        canActivate: [roleGuard],
        data: { roles: ['Admin'] },
        loadComponent: () => import('./features/billing/billing').then((m) => m.Billing),
      },
      {
        // SaaS owner's back office — hidden for everyone else
        path: 'platform',
        canActivate: [platformGuard],
        loadComponent: () => import('./features/platform/platform').then((m) => m.Platform),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
