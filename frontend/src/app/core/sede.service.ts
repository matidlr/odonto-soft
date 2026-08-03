import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface Sede {
  id: string;
  odontologoId: string;
  nombre: string;
  direccion: string | null;
  esPrincipal: boolean;
  activa: boolean;
}

export interface CrearSedeRequest {
  odontologoId: string;
  nombre: string;
  direccion?: string;
}

export interface EditarSedeRequest {
  nombre: string;
  direccion?: string;
  activa: boolean;
}

@Injectable({ providedIn: 'root' })
export class SedeService {
  constructor(private http: HttpClient) {}

  getAll(odontologoId?: string, incluirInactivas = false): Promise<Sede[]> {
    const params: Record<string, string | boolean> = { incluirInactivas };
    if (odontologoId) params['odontologoId'] = odontologoId;
    return firstValueFrom(this.http.get<Sede[]>(`${API_BASE_URL}/sedes`, { params }));
  }

  crear(datos: CrearSedeRequest): Promise<Sede> {
    return firstValueFrom(this.http.post<Sede>(`${API_BASE_URL}/sedes`, datos));
  }

  editar(id: string, datos: EditarSedeRequest): Promise<Sede> {
    return firstValueFrom(this.http.put<Sede>(`${API_BASE_URL}/sedes/${id}`, datos));
  }
}
