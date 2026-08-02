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
        path: 'pacientes/:pacienteId/odontograma',
        loadComponent: () =>
          import('./features/odontograma/odontograma.component').then(
            (m) => m.OdontogramaComponent
          )
      },
      {
        path: 'pacientes/:pacienteId/odontograma/:numeroFdi',
        loadComponent: () =>
          import('./features/odontograma/diente-detalle.component').then(
            (m) => m.DienteDetalleComponent
          )
      },
      {
        path: 'pacientes/:pacienteId/historial-clinico',
        loadComponent: () =>
          import('./features/historial-clinico/historial-clinico.component').then(
            (m) => m.HistorialClinicoComponent
          )
      },
      {
        path: 'pacientes/:pacienteId/archivos',
        loadComponent: () =>
          import('./features/archivos-paciente/archivos-paciente.component').then(
            (m) => m.ArchivosPacienteComponent
          )
      },
      {
        path: 'pacientes/:pacienteId/presupuestos',
        loadComponent: () =>
          import('./features/presupuestos/presupuestos.component').then(
            (m) => m.PresupuestosComponent
          )
      },
      {
        path: 'tratamientos',
        loadComponent: () =>
          import('./features/tratamientos/tratamientos.component').then(
            (m) => m.TratamientosComponent
          )
      },
      {
        path: 'disponibilidad',
        loadComponent: () =>
          import('./features/disponibilidad/disponibilidad.component').then(
            (m) => m.DisponibilidadComponent
          )
      },
      {
        path: 'odontologos',
        loadComponent: () =>
          import('./features/odontologos/odontologos.component').then(
            (m) => m.OdontologosComponent
          )
      },
      {
        path: 'plan',
        loadComponent: () => import('./features/plan/plan.component').then((m) => m.PlanComponent)
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
  },
  {
    path: 'olvide-password',
    loadComponent: () =>
      import('./features/auth/olvide-password.component').then((m) => m.OlvidePasswordComponent)
  },
  {
    path: 'resetear-password',
    loadComponent: () =>
      import('./features/auth/resetear-password.component').then(
        (m) => m.ResetearPasswordComponent
      )
  }
];
