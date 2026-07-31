import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type TurnoEstado = 'Solicitado' | 'Confirmado' | 'Cancelado' | 'Completado' | 'Ausente';

export interface Turno {
  id: string;
  odontologoId: string;
  pacienteId: string;
  pacienteNombre: string;
  tipoTratamientoId: string | null;
  fechaHora: string;
  duracionMinutos: number;
  estado: TurnoEstado;
}

export interface ReservarTurnoManualRequest {
  pacienteId: string;
  odontologoId: string;
  tipoTratamientoId?: string;
  fechaHora: string;
}

@Injectable({ providedIn: 'root' })
export class TurnoService {
  constructor(private http: HttpClient) {}

  getAll(desde?: Date, hasta?: Date, odontologoId?: string, pacienteId?: string): Promise<Turno[]> {
    const params: Record<string, string> = {};
    if (desde) params['desde'] = desde.toISOString();
    if (hasta) params['hasta'] = hasta.toISOString();
    if (odontologoId) params['odontologoId'] = odontologoId;
    if (pacienteId) params['pacienteId'] = pacienteId;

    return firstValueFrom(this.http.get<Turno[]>(`${API_BASE_URL}/turnos`, { params }));
  }

  crear(datos: ReservarTurnoManualRequest): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(`${API_BASE_URL}/turnos`, datos));
  }

  cambiarEstado(id: string, estado: TurnoEstado): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`${API_BASE_URL}/turnos/${id}/estado`, { estado })
    );
  }
}
