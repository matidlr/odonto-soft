import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';
import { EstadoDiente } from './odontograma.service';

export type EstadoPresupuesto = 'Pendiente' | 'Aprobado' | 'Rechazado';

export interface ItemPresupuesto {
  id: string;
  tipoTratamientoId: string | null;
  descripcion: string;
  numeroFdi: number | null;
  estadoDienteResultante: EstadoDiente | null;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

export interface Presupuesto {
  id: string;
  pacienteId: string;
  odontologoId: string | null;
  estado: EstadoPresupuesto;
  observaciones: string | null;
  convertido: boolean;
  fechaConversion: string | null;
  fechaCreacion: string;
  fechaRespuesta: string | null;
  montoTotal: number;
  items: ItemPresupuesto[];
}

export interface ItemPresupuestoRequest {
  tipoTratamientoId?: string;
  descripcion: string;
  numeroFdi?: number;
  estadoDienteResultante?: EstadoDiente;
  cantidad: number;
  precioUnitario: number;
}

export interface CrearPresupuestoRequest {
  odontologoId?: string;
  observaciones?: string;
  items: ItemPresupuestoRequest[];
}

@Injectable({ providedIn: 'root' })
export class PresupuestoService {
  constructor(private http: HttpClient) {}

  getPorPaciente(pacienteId: string): Promise<Presupuesto[]> {
    return firstValueFrom(
      this.http.get<Presupuesto[]>(`${API_BASE_URL}/pacientes/${pacienteId}/presupuestos`)
    );
  }

  crear(pacienteId: string, datos: CrearPresupuestoRequest): Promise<Presupuesto> {
    return firstValueFrom(
      this.http.post<Presupuesto>(`${API_BASE_URL}/pacientes/${pacienteId}/presupuestos`, datos)
    );
  }

  cambiarEstado(id: string, estado: 'Aprobado' | 'Rechazado'): Promise<Presupuesto> {
    return firstValueFrom(
      this.http.put<Presupuesto>(`${API_BASE_URL}/presupuestos/${id}/estado`, { estado })
    );
  }

  convertir(id: string): Promise<{ id: string; eventosCreados: number }> {
    return firstValueFrom(
      this.http.post<{ id: string; eventosCreados: number }>(`${API_BASE_URL}/presupuestos/${id}/convertir`, {})
    );
  }

  borrar(id: string): Promise<{ message: string }> {
    return firstValueFrom(
      this.http.delete<{ message: string }>(`${API_BASE_URL}/presupuestos/${id}`)
    );
  }
}
