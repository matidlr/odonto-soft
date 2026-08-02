import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface Odontologo {
  id: string;
  nombre: string;
  matricula: string;
  especialidad: string | null;
  colorAgenda: string;
}

export interface CrearOdontologoRequest {
  nombre: string;
  matricula: string;
  especialidad?: string;
  colorAgenda?: string;
}

@Injectable({ providedIn: 'root' })
export class OdontologoService {
  constructor(private http: HttpClient) {}

  getAll(): Promise<Odontologo[]> {
    return firstValueFrom(this.http.get<Odontologo[]>(`${API_BASE_URL}/odontologos`));
  }

  crear(datos: CrearOdontologoRequest): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(`${API_BASE_URL}/odontologos`, datos));
  }
}
