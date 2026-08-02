import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface TipoTratamiento {
  id: string;
  nombre: string;
  duracionMinutos: number;
  precioBase: number;
  observaciones: string | null;
}

export interface CrearTipoTratamientoRequest {
  nombre: string;
  duracionMinutos: number;
  precioBase: number;
  observaciones?: string;
}

export interface EditarTipoTratamientoRequest {
  nombre: string;
  duracionMinutos: number;
  precioBase: number;
  observaciones?: string;
}

@Injectable({ providedIn: 'root' })
export class TipoTratamientoService {
  constructor(private http: HttpClient) {}

  getAll(): Promise<TipoTratamiento[]> {
    return firstValueFrom(this.http.get<TipoTratamiento[]>(`${API_BASE_URL}/tipos-tratamiento`));
  }

  crear(datos: CrearTipoTratamientoRequest): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(`${API_BASE_URL}/tipos-tratamiento`, datos)
    );
  }

  editar(id: string, datos: EditarTipoTratamientoRequest): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.put<{ id: string }>(`${API_BASE_URL}/tipos-tratamiento/${id}`, datos)
    );
  }
}
