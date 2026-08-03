import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface Configuracion {
  nombre: string;
  direccion: string | null;
  telefono: string | null;
  emailContacto: string | null;
  tieneLogo: boolean;
}

export interface EditarConfiguracionRequest {
  nombre: string;
  direccion?: string;
  telefono?: string;
  emailContacto?: string;
}

@Injectable({ providedIn: 'root' })
export class ConfiguracionService {
  constructor(private http: HttpClient) {}

  get(): Promise<Configuracion> {
    return firstValueFrom(this.http.get<Configuracion>(`${API_BASE_URL}/configuracion`));
  }

  editar(datos: EditarConfiguracionRequest): Promise<Configuracion> {
    return firstValueFrom(this.http.put<Configuracion>(`${API_BASE_URL}/configuracion`, datos));
  }

  subirLogo(archivo: File): Promise<{ message: string }> {
    const formData = new FormData();
    formData.append('archivo', archivo);
    return firstValueFrom(
      this.http.post<{ message: string }>(`${API_BASE_URL}/configuracion/logo`, formData)
    );
  }

  getLogoBlob(): Promise<Blob> {
    return firstValueFrom(this.http.get(`${API_BASE_URL}/configuracion/logo`, { responseType: 'blob' }));
  }
}
