import { DecimalPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MiTenant, Plan, TenantService } from '../../core/tenant.service';
import { SuscripcionService } from '../../core/suscripcion.service';

@Component({
  selector: 'app-plan',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './plan.component.html',
  styleUrl: './plan.component.scss'
})
export class PlanComponent implements OnInit {
  tenant = signal<MiTenant | null>(null);
  planes = signal<Plan[]>([]);
  cargando = signal(true);
  suscribiendoId = signal<string | null>(null);
  error = signal<string | null>(null);
  vencioPrueba = signal(false);

  constructor(
    private route: ActivatedRoute,
    private tenantService: TenantService,
    private suscripcionService: SuscripcionService
  ) {}

  async ngOnInit(): Promise<void> {
    this.vencioPrueba.set(this.route.snapshot.queryParamMap.get('motivo') === 'suspendido');

    this.cargando.set(true);
    try {
      const [tenant, planes] = await Promise.all([
        this.tenantService.miTenant(),
        this.tenantService.getPlanes()
      ]);
      this.tenant.set(tenant);
      this.planes.set(planes);
    } finally {
      this.cargando.set(false);
    }
  }

  async suscribirse(plan: Plan): Promise<void> {
    this.error.set(null);
    this.suscribiendoId.set(plan.id);
    try {
      const { initPoint } = await this.suscripcionService.iniciarPago(plan.id);
      // Redirige al checkout seguro de Mercado Pago, donde carga la tarjeta.
      window.location.href = initPoint;
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(
        httpError?.error?.message ?? 'No se pudo iniciar el pago. Probá de nuevo en un momento.'
      );
      this.suscribiendoId.set(null);
    }
  }
}
