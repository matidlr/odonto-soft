import { Component, OnInit, signal } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { MiTenant, TenantService } from '../../core/tenant.service';

// Pantalla mínima post-login. Va a ser reemplazada por el dashboard real
// (agenda, pacientes, etc.) más adelante. Por ahora sirve para confirmar
// que el login funciona y para mostrar el estado de activación del tenant.
@Component({
  selector: 'app-home',
  standalone: true,
  template: `
    <div style="padding: 2rem; font-family: sans-serif;">
      <h1>¡Sesión iniciada!</h1>
      <p>Email: {{ auth.sesion()?.email }}</p>
      <p>Rol: {{ auth.sesion()?.rol }}</p>

      @if (tenant()) {
        <p>Clínica: {{ tenant()!.nombre }}</p>

        @if (tenant()!.estado === 'PendienteDeActivacion') {
          <div style="background:#fff3cd; border:1px solid #ffe69c; padding:1rem; border-radius:8px; max-width:480px;">
            <strong>Tu cuenta está pendiente de activación.</strong>
            <p>
              Podés ver la aplicación, pero todavía no vas a poder usar la agenda ni
              cargar pacientes hasta que actives tu suscripción o el SuperAdmin habilite tu cuenta.
            </p>
          </div>
        } @else if (tenant()!.estado === 'Suspendido') {
          <div style="background:#f8d7da; border:1px solid #f1aeb5; padding:1rem; border-radius:8px; max-width:480px;">
            <strong>Tu cuenta está suspendida.</strong>
          </div>
        } @else {
          <p style="color: #16803c;">Cuenta activa ✓</p>
        }
      } @else if (auth.sesion()?.tenantId) {
        <p>Cargando datos de la clínica...</p>
      } @else {
        <p>(SuperAdmin, sin tenant asociado)</p>
      }

      <button (click)="auth.logout()">Cerrar sesión</button>
    </div>
  `
})
export class HomeComponent implements OnInit {
  tenant = signal<MiTenant | null>(null);

  constructor(
    public auth: AuthService,
    private tenantService: TenantService
  ) {}

  async ngOnInit(): Promise<void> {
    if (this.auth.sesion()?.tenantId) {
      this.tenant.set(await this.tenantService.miTenant());
    }
  }
}
