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

@Injectable({ providedIn: 'root' })
export class OdontologoService {
  constructor(private http: HttpClient) {}

  getAll(): Promise<Odontologo[]> {
    return firstValueFrom(this.http.get<Odontologo[]>(`${API_BASE_URL}/odontologos`));
  }
}
