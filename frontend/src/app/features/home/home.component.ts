import { Component, OnInit, signal } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { MiTenant, TenantService } from '../../core/tenant.service';

@Component({
  selector: 'app-home',
  standalone: true,
  template: `
    <h1>Inicio</h1>
    <p>Rol: {{ auth.sesion()?.rol }}</p>

    @if (tenant()) {
      <p>Clínica: {{ tenant()!.nombre }}</p>

      @if (tenant()!.estado === 'PendienteDeActivacion') {
        <div class="aviso aviso-pendiente">
          <strong>Tu cuenta está pendiente de activación.</strong>
          <p>
            Podés ver la aplicación, pero todavía no vas a poder usar la agenda ni
            cargar pacientes hasta que actives tu suscripción o el SuperAdmin habilite tu cuenta.
          </p>
        </div>
      } @else if (tenant()!.estado === 'Suspendido') {
        <div class="aviso aviso-suspendido">
          <strong>Tu cuenta está suspendida.</strong>
        </div>
      } @else {
        <p class="aviso-ok">Cuenta activa ✓</p>
      }
    } @else if (auth.sesion()?.tenantId) {
      <p>Cargando datos de la clínica...</p>
    } @else {
      <p>(SuperAdmin, sin tenant asociado)</p>
    }
  `,
  styles: `
    .aviso { padding: 1rem; border-radius: 8px; max-width: 480px; }
    .aviso-pendiente { background: #fff3cd; border: 1px solid #ffe69c; }
    .aviso-suspendido { background: #f8d7da; border: 1px solid #f1aeb5; }
    .aviso-ok { color: #16803c; }
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
