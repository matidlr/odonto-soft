import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, effect, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { MiTenant, TenantService } from '../../core/tenant.service';
import { Turno, TurnoService } from '../../core/turno.service';

function inicioDeHoy(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}

function finDeHoy(): Date {
  const d = new Date();
  d.setHours(23, 59, 59, 999);
  return d;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  tenant = signal<MiTenant | null>(null);
  turnosHoy = signal<Turno[]>([]);
  pendientesDeConfirmar = signal<Turno[]>([]);
  cargandoTurnos = signal(false);

  cantidadHoy = computed(() => this.turnosHoy().length);

  constructor(
    public auth: AuthService,
    private tenantService: TenantService,
    private turnoService: TurnoService,
    public contexto: OdontologoContextoService
  ) {
    // Si cambian el odontólogo en el navbar, refrescamos el resumen del día.
    effect(() => {
      this.contexto.seleccionadoId();
      if (this.tenant()?.estado === 'Activo') {
        this.cargarResumenDelDia();
      }
    });
  }

  async ngOnInit(): Promise<void> {
    if (!this.auth.sesion()?.tenantId) return;

    this.tenant.set(await this.tenantService.miTenant());

    if (this.tenant()?.estado === 'Activo') {
      await this.cargarResumenDelDia();
    }
  }

  private async cargarResumenDelDia(): Promise<void> {
    this.cargandoTurnos.set(true);
    try {
      const odontologoId = this.contexto.seleccionadoId() ?? undefined;
      const [hoy, proximos30Dias] = await Promise.all([
        this.turnoService.getAll(inicioDeHoy(), finDeHoy(), odontologoId),
        this.turnoService.getAll(
          inicioDeHoy(),
          new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
          odontologoId
        )
      ]);
      this.turnosHoy.set(hoy.filter((t) => t.estado !== 'Cancelado'));
      this.pendientesDeConfirmar.set(proximos30Dias.filter((t) => t.estado === 'Solicitado'));
    } finally {
      this.cargandoTurnos.set(false);
    }
  }
}
