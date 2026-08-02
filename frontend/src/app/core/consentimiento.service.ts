import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type TipoConsentimiento = 'ConsentimientoInformado' | 'Cirugia' | 'Implante' | 'Otro';

export interface Consentimiento {
  id: string;
  pacienteId: string;
  odontologoId: string | null;
  tipo: TipoConsentimiento;
  titulo: string;
  texto: string;
  firmaBase64: string | null;
  firmaNombreAclaratorio: string | null;
  fechaFirma: string | null;
  firmado: boolean;
  fechaCreacion: string;
}

export interface CrearConsentimientoRequest {
  tipo: TipoConsentimiento;
  titulo: string;
  texto: string;
  odontologoId?: string;
  firmaBase64?: string;
  firmaNombreAclaratorio?: string;
}

export interface FirmarConsentimientoRequest {
  firmaBase64: string;
  firmaNombreAclaratorio?: string;
}

@Injectable({ providedIn: 'root' })
export class ConsentimientoService {
  constructor(private http: HttpClient) {}

  getPorPaciente(pacienteId: string): Promise<Consentimiento[]> {
    return firstValueFrom(
      this.http.get<Consentimiento[]>(`${API_BASE_URL}/pacientes/${pacienteId}/consentimientos`)
    );
  }

  crear(pacienteId: string, datos: CrearConsentimientoRequest): Promise<Consentimiento> {
    return firstValueFrom(
      this.http.post<Consentimiento>(`${API_BASE_URL}/pacientes/${pacienteId}/consentimientos`, datos)
    );
  }

  firmar(id: string, datos: FirmarConsentimientoRequest): Promise<Consentimiento> {
    return firstValueFrom(
      this.http.post<Consentimiento>(`${API_BASE_URL}/consentimientos/${id}/firmar`, datos)
    );
  }

  borrar(id: string): Promise<{ message: string }> {
    return firstValueFrom(this.http.delete<{ message: string }>(`${API_BASE_URL}/consentimientos/${id}`));
  }
}
