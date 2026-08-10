import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface IniciarPagoResponse {
  initPoint: string;
  preapprovalId: string;
}

export interface SincronizarEstadoResponse {
  estado: string;
  tienePagoActivo: boolean;
  estadoMercadoPago: string;
}

@Injectable({ providedIn: 'root' })
export class SuscripcionService {
  constructor(private http: HttpClient) {}

  iniciarPago(planId: string): Promise<IniciarPagoResponse> {
    return firstValueFrom(
      this.http.post<IniciarPagoResponse>(`${API_BASE_URL}/suscripcion/iniciar-pago`, { planId })
    );
  }

  // Le pregunta a Mercado Pago el estado real de la suscripción, en vez de
  // esperar a que llegue el webhook. Sirve para "ya pagué, ¿por qué sigo
  // suspendido?" mientras el aviso automático todavía no llegó (o, en
  // desarrollo local, nunca va a llegar porque no hay URL pública).
  sincronizarEstado(): Promise<SincronizarEstadoResponse> {
    return firstValueFrom(
      this.http.post<SincronizarEstadoResponse>(`${API_BASE_URL}/suscripcion/sincronizar-estado`, {})
    );
  }
}
