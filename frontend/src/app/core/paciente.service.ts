import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface Paciente {
  id: string;
  nombre: string;
  dni: string | null;
  telefono: string | null;
  email: string | null;
  fechaNacimiento: string | null;
  odontologoPrincipalId: string | null;
  activo: boolean;
}

export interface CrearPacienteRequest {
  nombre: string;
  dni?: string;
  telefono?: string;
  email?: string;
  fechaNacimiento?: string;
  odontologoPrincipalId?: string;
}

@Injectable({ providedIn: 'root' })
export class PacienteService {
  constructor(private http: HttpClient) {}

  getAll(odontologoId?: string, incluirInactivos = false): Promise<Paciente[]> {
    const params: Record<string, string> = {};
    if (odontologoId) params['odontologoId'] = odontologoId;
    if (incluirInactivos) params['incluirInactivos'] = 'true';
    return firstValueFrom(this.http.get<Paciente[]>(`${API_BASE_URL}/pacientes`, { params }));
  }

  crear(datos: CrearPacienteRequest): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(`${API_BASE_URL}/pacientes`, datos));
  }

  editar(id: string, datos: CrearPacienteRequest): Promise<{ id: string }> {
    return firstValueFrom(this.http.put<{ id: string }>(`${API_BASE_URL}/pacientes/${id}`, datos));
  }

  eliminar(id: string): Promise<{ message: string }> {
    return firstValueFrom(this.http.delete<{ message: string }>(`${API_BASE_URL}/pacientes/${id}`));
  }

  reactivar(id: string): Promise<{ message: string }> {
    return firstValueFrom(this.http.post<{ message: string }>(`${API_BASE_URL}/pacientes/${id}/reactivar`, {}));
  }
}
