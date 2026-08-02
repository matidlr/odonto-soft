import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export type CategoriaArchivo = 'Radiografia' | 'Foto' | 'Estudio' | 'Documento';

export interface ArchivoPaciente {
  id: string;
  categoria: CategoriaArchivo;
  descripcion: string | null;
  nombreOriginal: string;
  contentType: string;
  tamanioBytes: number;
  fechaSubida: string;
}

@Injectable({ providedIn: 'root' })
export class ArchivoPacienteService {
  constructor(private http: HttpClient) {}

  getArchivos(pacienteId: string): Promise<ArchivoPaciente[]> {
    return firstValueFrom(
      this.http.get<ArchivoPaciente[]>(`${API_BASE_URL}/pacientes/${pacienteId}/archivos`)
    );
  }

  subirArchivo(
    pacienteId: string,
    archivo: File,
    categoria: CategoriaArchivo,
    descripcion?: string
  ): Promise<ArchivoPaciente> {
    const formData = new FormData();
    formData.append('archivo', archivo);
    formData.append('categoria', categoria);
    if (descripcion) formData.append('descripcion', descripcion);
    // OJO: no seteamos el header Content-Type a mano — el browser arma el
    // multipart/form-data con el boundary correcto solo si se lo dejamos.
    return firstValueFrom(
      this.http.post<ArchivoPaciente>(`${API_BASE_URL}/pacientes/${pacienteId}/archivos`, formData)
    );
  }

  descargarArchivo(pacienteId: string, archivoId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${API_BASE_URL}/pacientes/${pacienteId}/archivos/${archivoId}`, { responseType: 'blob' })
    );
  }

  borrarArchivo(pacienteId: string, archivoId: string): Promise<{ message: string }> {
    return firstValueFrom(
      this.http.delete<{ message: string }>(`${API_BASE_URL}/pacientes/${pacienteId}/archivos/${archivoId}`)
    );
  }
}
