import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type DiaSemana = 'Lunes' | 'Martes' | 'Miercoles' | 'Jueves' | 'Viernes' | 'Sabado' | 'Domingo';
export type TipoDisponibilidad = 'Recurrente' | 'Excepcion';

export interface Disponibilidad {
  id: string;
  odontologoId: string;
  tipo: TipoDisponibilidad;
  diaSemana: DiaSemana | null;
  fecha: string | null;
  todoElDia: boolean;
  horaInicio: string | null;
  horaFin: string | null;
  bloqueado: boolean;
}

export interface CrearDisponibilidadRequest {
  odontologoId: string;
  tipo: TipoDisponibilidad;
  diaSemana?: DiaSemana;
  fecha?: string;
  todoElDia: boolean;
  horaInicio?: string;
  horaFin?: string;
  bloqueado: boolean;
}

@Injectable({ providedIn: 'root' })
export class DisponibilidadService {
  constructor(private http: HttpClient) {}

  getAll(odontologoId?: string): Promise<Disponibilidad[]> {
    const params: Record<string, string> = {};
    if (odontologoId) params['odontologoId'] = odontologoId;
    return firstValueFrom(
      this.http.get<Disponibilidad[]>(`${API_BASE_URL}/disponibilidad`, { params })
    );
  }

  crear(datos: CrearDisponibilidadRequest): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(`${API_BASE_URL}/disponibilidad`, datos)
    );
  }

  eliminar(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/disponibilidad/${id}`));
  }
}
