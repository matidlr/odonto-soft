import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Plan, TenantResumen, TenantService } from '../../core/tenant.service';

@Component({
  selector: 'app-admin-tenants',
  standalone: true,
  imports: [DatePipe, FormsModule],
  templateUrl: './tenants.component.html',
  styleUrl: './tenants.component.scss'
})
export class AdminTenantsComponent implements OnInit {
  tenants = signal<TenantResumen[]>([]);
  planes = signal<Plan[]>([]);
  cargando = signal(true);
  error = signal<string | null>(null);
  procesandoId = signal<string | null>(null);

  constructor(private tenantService: TenantService) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.cargar(), this.cargarPlanes()]);
  }

  async cargarPlanes(): Promise<void> {
    try {
      this.planes.set(await this.tenantService.getPlanes());
    } catch {
      // Si falla, el selector de plan queda vacío pero el resto del panel sigue andando.
    }
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    try {
      this.tenants.set(await this.tenantService.getAll());
    } catch {
      this.error.set('No se pudo cargar la lista de tenants (¿sos SuperAdmin?).');
    } finally {
      this.cargando.set(false);
    }
  }

  async cambiarPlan(t: TenantResumen, planId: string): Promise<void> {
    if (!planId || planId === t.planId) return;
    this.procesandoId.set(t.id);
    this.error.set(null);
    try {
      await this.tenantService.cambiarPlan(t.id, planId);
      await this.cargar();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo cambiar el plan.');
    } finally {
      this.procesandoId.set(null);
    }
  }

  async activar(t: TenantResumen): Promise<void> {
    this.procesandoId.set(t.id);
    try {
      await this.tenantService.activar(t.id);
      await this.cargar();
    } catch {
      this.error.set('No se pudo activar el tenant.');
    } finally {
      this.procesandoId.set(null);
    }
  }

  async suspender(t: TenantResumen): Promise<void> {
    this.procesandoId.set(t.id);
    try {
      await this.tenantService.suspender(t.id);
      await this.cargar();
    } catch {
      this.error.set('No se pudo suspender el tenant.');
    } finally {
      this.procesandoId.set(null);
    }
  }
}
