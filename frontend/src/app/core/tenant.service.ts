import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface MiTenant {
  id: string;
  nombre: string;
  slug: string;
  estado: 'PendienteDeActivacion' | 'Activo' | 'Suspendido' | string;
  enPrueba: boolean;
  diasRestantesDePrueba: number;
  tienePagoActivo: boolean;
  planId: string | null;
  planNombre: string | null;
}

export interface TenantResumen {
  id: string;
  nombre: string;
  slug: string;
  estado: string;
  fechaAlta: string;
  planId: string | null;
  planNombre: string | null;
  maxOdontologos: number | null;
  cantidadOdontologos: number;
  fechaFinPrueba: string | null;
  tienePagoActivo: boolean;
}

export interface Plan {
  id: string;
  nombre: string;
  maxOdontologos: number;
  precioMensual: number;
}

@Injectable({ providedIn: 'root' })
export class TenantService {
  constructor(private http: HttpClient) {}

  // Devuelve null si el usuario logueado no pertenece a ningún tenant
  // (caso SuperAdmin), en vez de dejar que explote el 404 del backend.
  async miTenant(): Promise<MiTenant | null> {
    try {
      return await firstValueFrom(this.http.get<MiTenant>(`${API_BASE_URL}/tenants/mi-tenant`));
    } catch {
      return null;
    }
  }

  // Estos son solo para SuperAdmin (el backend los rechaza para
  // cualquier otro rol con 403).
  getAll(): Promise<TenantResumen[]> {
    return firstValueFrom(this.http.get<TenantResumen[]>(`${API_BASE_URL}/tenants`));
  }

  getPlanes(): Promise<Plan[]> {
    return firstValueFrom(this.http.get<Plan[]>(`${API_BASE_URL}/planes`));
  }

  cambiarPlan(id: string, planId: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${API_BASE_URL}/tenants/${id}/plan`, { planId }));
  }

  activar(id: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${API_BASE_URL}/tenants/${id}/activar`, {}));
  }

  suspender(id: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${API_BASE_URL}/tenants/${id}/suspender`, {}));
  }
}
