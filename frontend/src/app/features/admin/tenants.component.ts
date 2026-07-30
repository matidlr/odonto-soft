import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { TenantResumen, TenantService } from '../../core/tenant.service';

@Component({
  selector: 'app-admin-tenants',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './tenants.component.html',
  styleUrl: './tenants.component.scss'
})
export class AdminTenantsComponent implements OnInit {
  tenants = signal<TenantResumen[]>([]);
  cargando = signal(true);
  error = signal<string | null>(null);
  procesandoId = signal<string | null>(null);

  constructor(private tenantService: TenantService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
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
