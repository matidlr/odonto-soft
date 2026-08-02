import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type TurnoEstado = 'Solicitado' | 'Confirmado' | 'Cancelado' | 'Completado' | 'Ausente';

export interface FacturacionMes {
  anio: number;
  mes: number;
  total: number;
}

export interface TratamientoRanking {
  nombre: string;
  cantidad: number;
}

export interface OdontologoRanking {
  odontologoId: string;
  nombre: string;
  cantidad: number;
}

export interface TurnosPorEstado {
  estado: TurnoEstado;
  cantidad: number;
}

export interface Estadisticas {
  desde: string;
  hasta: string;
  pacientesNuevos: number;
  cantidadTurnos: number;
  cancelaciones: number;
  turnosPorEstado: TurnosPorEstado[];
  facturacionTotalPeriodo: number;
  facturacionPorMes: FacturacionMes[];
  tratamientosMasRealizados: TratamientoRanking[];
  odontologosConMasConsultas: OdontologoRanking[];
}

@Injectable({ providedIn: 'root' })
export class EstadisticaService {
  constructor(private http: HttpClient) {}

  get(desde?: string, hasta?: string): Promise<Estadisticas> {
    const params: Record<string, string> = {};
    if (desde) params['desde'] = desde;
    if (hasta) params['hasta'] = hasta;
    return firstValueFrom(this.http.get<Estadisticas>(`${API_BASE_URL}/estadisticas`, { params }));
  }
}
