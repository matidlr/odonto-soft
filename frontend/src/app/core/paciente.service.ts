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
}

export interface CrearPacienteRequest {
  nombre: string;
  dni?: string;
  telefono?: string;
  email?: string;
  fechaNacimiento?: string;
}

@Injectable({ providedIn: 'root' })
export class PacienteService {
  constructor(private http: HttpClient) {}

  getAll(): Promise<Paciente[]> {
    return firstValueFrom(this.http.get<Paciente[]>(`${API_BASE_URL}/pacientes`));
  }

  crear(datos: CrearPacienteRequest): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(`${API_BASE_URL}/pacientes`, datos));
  }
}
