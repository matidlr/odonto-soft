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

  logout(motivo?: 'inactividad'): void {
    // Mejor esfuerzo: avisamos al backend para que revoque el refresh token
    // de esta sesión y borre la cookie. Si falla (sin conexión, etc.) igual
    // cerramos la sesión local, que es lo que importa para el usuario.
    firstValueFrom(this.http.post(`${API_BASE_URL}/auth/logout`, {})).catch(() => {});

    localStorage.removeItem(STORAGE_KEY);
    this.sesionSignal.set(null);
    this.router.navigate(['/login'], { queryParams: motivo ? { motivo } : {} });
  }

  /// Cierra la sesión en todos los dispositivos (revoca todos los refresh
  /// tokens del usuario, no solo el de esta pestaña) y termina también la
  /// sesión local.
  async logoutTodos(): Promise<string> {
    const response = await firstValueFrom(
      this.http.post<{ message: string }>(`${API_BASE_URL}/auth/logout-todos`, {})
    );
    localStorage.removeItem(STORAGE_KEY);
    this.sesionSignal.set(null);
    this.router.navigateByUrl('/login');
    return response.message;
  }

  // Usa el refresh token (cookie httpOnly, el navegador lo manda solo) para
  // conseguir un access token nuevo sin que el usuario tenga que loguearse
  // de nuevo. Devuelve null si el refresh token también venció o es
  // inválido (ahí sí hay que loguearse de nuevo).
  async refrescarToken(): Promise<string | null> {
    try {
      const response = await firstValueFrom(
        this.http.post<LoginResponse>(`${API_BASE_URL}/auth/refresh`, {})
      );
      const sesionActual = this.sesionSignal();
      this.guardarSesion({ ...response, email: sesionActual?.email ?? '' });
      return response.token;
    } catch {
      return null;
    }
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
