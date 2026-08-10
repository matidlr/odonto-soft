import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface Paciente {
  id: string;
  nombre: string;
  // Opcional: los pacientes cargados antes de que existiera este campo
  // (o dados de alta por el link público, que no lo pide) pueden no
  // tenerlo cargado.
  apellido: string | null;
  dni: string | null;
  telefono: string | null;
  email: string | null;
  fechaNacimiento: string | null;
  odontologoPrincipalId: string | null;
  activo: boolean;
  // Solo vienen en getById (la lista no los necesita).
  fechaRegistro?: string;
  ultimaVisita?: string | null;
}

export interface CrearPacienteRequest {
  nombre: string;
  apellido?: string;
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

  getById(id: string): Promise<Paciente> {
    return firstValueFrom(this.http.get<Paciente>(`${API_BASE_URL}/pacientes/${id}`));
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
