import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/panel-layout.component').then((m) => m.PanelLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'inicio' },
      {
        path: 'inicio',
        loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent)
      },
      {
        path: 'agenda',
        loadComponent: () =>
          import('./features/agenda/agenda.component').then((m) => m.AgendaComponent)
      },
      {
        path: 'pacientes',
        loadComponent: () =>
          import('./features/pacientes/pacientes.component').then((m) => m.PacientesComponent)
      },
      {
        path: 'admin/tenants',
        loadComponent: () =>
          import('./features/admin/tenants.component').then((m) => m.AdminTenantsComponent)
      }
    ]
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'registro',
    loadComponent: () =>
      import('./features/auth/registro.component').then((m) => m.RegistroComponent)
  }
];
