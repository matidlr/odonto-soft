import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type EstadoDiente =
  | 'Sano'
  | 'Cariado'
  | 'Obturado'
  | 'Corona'
  | 'Endodoncia'
  | 'Ausente'
  | 'Implante'
  | 'Fracturado'
  | 'Sellador'
  | 'Ortodoncia';

export type EstadoTratamiento = 'Planificado' | 'Realizado';

export interface EstadoPieza {
  numeroFdi: number;
  estado: EstadoDiente;
  estadoTratamiento: EstadoTratamiento | null;
  fecha: string | null;
  tratamiento: string | null;
  nota: string | null;
}

export interface ArchivoOdontograma {
  id: string;
  nombreOriginal: string;
  contentType: string;
  tamanioBytes: number;
  fechaSubida: string;
}

export interface EventoOdontograma {
  id: string;
  numeroFdi: number;
  estado: EstadoDiente;
  estadoTratamiento: EstadoTratamiento;
  tratamiento: string | null;
  nota: string | null;
  odontologoId: string | null;
  turnoId: string | null;
  fecha: string;
  archivos: ArchivoOdontograma[];
}

export interface CrearEventoRequest {
  numeroFdi: number;
  estado: EstadoDiente;
  estadoTratamiento?: EstadoTratamiento;
  tratamiento?: string;
  nota?: string;
  odontologoId?: string;
  turnoId?: string;
  fecha?: string;
}

@Injectable({ providedIn: 'root' })
export class OdontogramaService {
  constructor(private http: HttpClient) {}

  getEstadoActual(pacienteId: string): Promise<EstadoPieza[]> {
    return firstValueFrom(this.http.get<EstadoPieza[]>(`${API_BASE_URL}/odontograma/${pacienteId}`));
  }

  getHistorial(pacienteId: string, numeroFdi?: number): Promise<EventoOdontograma[]> {
    const params: Record<string, string> = {};
    if (numeroFdi) params['numeroFdi'] = String(numeroFdi);
    return firstValueFrom(
      this.http.get<EventoOdontograma[]>(`${API_BASE_URL}/odontograma/${pacienteId}/historial`, {
        params
      })
    );
  }

  crearEvento(pacienteId: string, datos: CrearEventoRequest): Promise<{ id: string; fecha: string }> {
    return firstValueFrom(
      this.http.post<{ id: string; fecha: string }>(
        `${API_BASE_URL}/odontograma/${pacienteId}/eventos`,
        datos
      )
    );
  }

  subirArchivo(eventoId: string, archivo: File): Promise<ArchivoOdontograma> {
    const formData = new FormData();
    formData.append('archivo', archivo);
    // OJO: no seteamos el header Content-Type a mano — el browser arma el
    // multipart/form-data con el boundary correcto solo si se lo dejamos.
    return firstValueFrom(
      this.http.post<ArchivoOdontograma>(
        `${API_BASE_URL}/odontograma/eventos/${eventoId}/archivos`,
        formData
      )
    );
  }

  descargarArchivo(archivoId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${API_BASE_URL}/odontograma/archivos/${archivoId}`, { responseType: 'blob' })
    );
  }
}
