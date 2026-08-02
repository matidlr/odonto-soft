import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type CategoriaInsumo = 'Anestesia' | 'Guantes' | 'Resinas' | 'Implantes' | 'Materiales' | 'Otro';

export interface Insumo {
  id: string;
  nombre: string;
  categoria: CategoriaInsumo;
  unidad: string;
  stockActual: number;
  stockMinimo: number;
  stockBajo: boolean;
  activo: boolean;
}

export interface CrearInsumoRequest {
  nombre: string;
  categoria: CategoriaInsumo;
  unidad: string;
  stockMinimo: number;
  stockInicial: number;
}

export interface EditarInsumoRequest {
  nombre: string;
  categoria: CategoriaInsumo;
  unidad: string;
  stockMinimo: number;
  activo: boolean;
}

export interface MovimientoStock {
  id: string;
  cantidad: number;
  motivo: string | null;
  fecha: string;
}

export interface CrearMovimientoRequest {
  cantidad: number;
  motivo?: string;
}

@Injectable({ providedIn: 'root' })
export class InsumoService {
  constructor(private http: HttpClient) {}

  getAll(incluirInactivos = false): Promise<Insumo[]> {
    return firstValueFrom(
      this.http.get<Insumo[]>(`${API_BASE_URL}/insumos`, { params: { incluirInactivos } })
    );
  }

  getAlertas(): Promise<Insumo[]> {
    return firstValueFrom(this.http.get<Insumo[]>(`${API_BASE_URL}/insumos/alertas`));
  }

  crear(datos: CrearInsumoRequest): Promise<Insumo> {
    return firstValueFrom(this.http.post<Insumo>(`${API_BASE_URL}/insumos`, datos));
  }

  editar(id: string, datos: EditarInsumoRequest): Promise<Insumo> {
    return firstValueFrom(this.http.put<Insumo>(`${API_BASE_URL}/insumos/${id}`, datos));
  }

  getMovimientos(id: string): Promise<MovimientoStock[]> {
    return firstValueFrom(this.http.get<MovimientoStock[]>(`${API_BASE_URL}/insumos/${id}/movimientos`));
  }

  crearMovimiento(id: string, datos: CrearMovimientoRequest): Promise<{ insumo: Insumo; movimiento: MovimientoStock }> {
    return firstValueFrom(
      this.http.post<{ insumo: Insumo; movimiento: MovimientoStock }>(`${API_BASE_URL}/insumos/${id}/movimientos`, datos)
    );
  }
}
