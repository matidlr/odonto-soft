import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type MedioPago = 'Efectivo' | 'Transferencia' | 'Tarjeta' | 'Qr';

export interface Cobro {
  id: string;
  pacienteId: string;
  presupuestoId: string | null;
  odontologoId: string | null;
  monto: number;
  medioPago: MedioPago;
  concepto: string | null;
  fecha: string;
}

export interface SaldoPaciente {
  totalAprobado: number;
  totalCobrado: number;
  saldo: number;
}

export interface CrearCobroRequest {
  monto: number;
  medioPago: MedioPago;
  concepto?: string;
  presupuestoId?: string;
  odontologoId?: string;
  fecha?: string;
}

export interface PacientePendiente {
  pacienteId: string;
  pacienteNombre: string;
  totalAprobado: number;
  totalCobrado: number;
  saldo: number;
}

@Injectable({ providedIn: 'root' })
export class CobroService {
  constructor(private http: HttpClient) {}

  getPorPaciente(pacienteId: string): Promise<Cobro[]> {
    return firstValueFrom(this.http.get<Cobro[]>(`${API_BASE_URL}/pacientes/${pacienteId}/cobros`));
  }

  getSaldo(pacienteId: string): Promise<SaldoPaciente> {
    return firstValueFrom(this.http.get<SaldoPaciente>(`${API_BASE_URL}/pacientes/${pacienteId}/saldo`));
  }

  crear(pacienteId: string, datos: CrearCobroRequest): Promise<Cobro> {
    return firstValueFrom(this.http.post<Cobro>(`${API_BASE_URL}/pacientes/${pacienteId}/cobros`, datos));
  }

  borrar(id: string): Promise<{ message: string }> {
    return firstValueFrom(this.http.delete<{ message: string }>(`${API_BASE_URL}/cobros/${id}`));
  }

  getPendientes(): Promise<PacientePendiente[]> {
    return firstValueFrom(this.http.get<PacientePendiente[]>(`${API_BASE_URL}/cobros/pendientes`));
  }
}
