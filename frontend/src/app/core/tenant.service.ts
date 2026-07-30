import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface MiTenant {
  id: string;
  nombre: string;
  slug: string;
  estado: 'PendienteDeActivacion' | 'Activo' | 'Suspendido' | string;
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
}
