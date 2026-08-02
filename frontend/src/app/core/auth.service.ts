import { HttpClient } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from './config';

export interface DatosSesion {
  token: string;
  email: string;
  rol: string;
  tenantId: string | null;
}

// Lo que realmente devuelve POST /api/auth/login (no incluye el email:
// ya lo sabemos porque lo mandamos nosotros en el request).
interface LoginResponse {
  token: string;
  rol: string;
  tenantId: string | null;
}

interface RegistrarOdontologoResponse {
  tenantId: string;
  estado: string;
}

const STORAGE_KEY = 'odonto_sesion';

// Servicio de autenticación: hace login/registro contra la API, guarda el
// JWT en localStorage (para no perder la sesión al refrescar la página) y
// expone la sesión actual como signal para que el resto de la app reaccione
// a login/logout sin tener que suscribirse manualmente a un Observable.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private sesionSignal = signal<DatosSesion | null>(this.leerSesionGuardada());

  sesion = this.sesionSignal.asReadonly();
  estaLogueado = computed(() => this.sesionSignal() !== null);

  constructor(
    private http: HttpClient,
    private router: Router
  ) {}

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${API_BASE_URL}/auth/login`, { email, password })
    );
    this.guardarSesion({ ...response, email });
  }

  // Registra el odontólogo/clínica nueva. OJO: esto NO inicia sesión solo
  // (la API no devuelve token acá) — hay que llamar a login() después con
  // las mismas credenciales.
  async registrarOdontologo(datos: {
    nombreClinica: string;
    slug: string;
    nombreOdontologo: string;
    email: string;
    password: string;
    matricula: string;
    especialidad?: string;
  }): Promise<RegistrarOdontologoResponse> {
    return firstValueFrom(
      this.http.post<RegistrarOdontologoResponse>(`${API_BASE_URL}/auth/registrar-odontologo`, datos)
    );
  }

  async olvidePassword(email: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/auth/olvide-password`, { email })
    );
  }

  async resetearPassword(token: string, newPassword: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/auth/resetear-password`, { token, newPassword })
    );
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.sesionSignal.set(null);
    this.router.navigateByUrl('/login');
  }

  private guardarSesion(sesion: DatosSesion): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sesion));
    this.sesionSignal.set(sesion);
  }

  private leerSesionGuardada(): DatosSesion | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as DatosSesion;
    } catch {
      return null;
    }
  }
}
