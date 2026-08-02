import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface IniciarPagoResponse {
  initPoint: string;
  preapprovalId: string;
}

@Injectable({ providedIn: 'root' })
export class SuscripcionService {
  constructor(private http: HttpClient) {}

  iniciarPago(planId: string): Promise<IniciarPagoResponse> {
    return firstValueFrom(
      this.http.post<IniciarPagoResponse>(`${API_BASE_URL}/suscripcion/iniciar-pago`, { planId })
    );
  }
}
