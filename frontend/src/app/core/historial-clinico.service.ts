import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface FichaMedica {
  alergias: string | null;
  enfermedadesPreexistentes: string | null;
  medicacionActual: string | null;
  habitos: string | null;
  observaciones: string | null;
  fechaActualizacion: string | null;
}

export interface GuardarFichaMedicaRequest {
  alergias?: string;
  enfermedadesPreexistentes?: string;
  medicacionActual?: string;
  habitos?: string;
  observaciones?: string;
}

export interface NotaEvolucion {
  id: string;
  motivo: string | null;
  diagnostico: string | null;
  tratamientoRealizado: string | null;
  evolucion: string | null;
  medicacion: string | null;
  observaciones: string | null;
  odontologoId: string | null;
  turnoId: string | null;
  fecha: string;
}

export interface CrearNotaEvolucionRequest {
  motivo?: string;
  diagnostico?: string;
  tratamientoRealizado?: string;
  evolucion?: string;
  medicacion?: string;
  observaciones?: string;
  odontologoId?: string;
  turnoId?: string;
  fecha?: string;
}

@Injectable({ providedIn: 'root' })
export class HistorialClinicoService {
  constructor(private http: HttpClient) {}

  getFichaMedica(pacienteId: string): Promise<FichaMedica> {
    return firstValueFrom(
      this.http.get<FichaMedica>(`${API_BASE_URL}/pacientes/${pacienteId}/ficha-medica`)
    );
  }

  guardarFichaMedica(pacienteId: string, datos: GuardarFichaMedicaRequest): Promise<{ message: string }> {
    return firstValueFrom(
      this.http.put<{ message: string }>(
        `${API_BASE_URL}/pacientes/${pacienteId}/ficha-medica`,
        datos
      )
    );
  }

  getNotasEvolucion(pacienteId: string): Promise<NotaEvolucion[]> {
    return firstValueFrom(
      this.http.get<NotaEvolucion[]>(`${API_BASE_URL}/pacientes/${pacienteId}/notas-evolucion`)
    );
  }

  crearNotaEvolucion(
    pacienteId: string,
    datos: CrearNotaEvolucionRequest
  ): Promise<{ id: string; fecha: string; turnoId: string | null }> {
    return firstValueFrom(
      this.http.post<{ id: string; fecha: string; turnoId: string | null }>(
        `${API_BASE_URL}/pacientes/${pacienteId}/notas-evolucion`,
        datos
      )
    );
  }
}
