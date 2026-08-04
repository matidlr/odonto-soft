import { Injectable, NgZone, signal } from '@angular/core';
import { AuthService } from './auth.service';

const EVENTOS_ACTIVIDAD = ['mousedown', 'mousemove', 'keydown', 'scroll', 'touchstart', 'click'];

// Después de 60 minutos sin ninguna interacción, se cierra la sesión sola
// (checklist de seguridad, "Expiración de sesión"). El aviso aparece 1
// minuto antes, con la chance de seguir conectado.
const MINUTOS_INACTIVIDAD = 60;
const SEGUNDOS_AVISO = 60;

// Corre en toda la app (arrancado desde AppComponent) mientras haya sesión
// iniciada: escucha cualquier interacción del usuario para saber que sigue
// activo, y si pasa mucho tiempo sin ninguna, cierra la sesión sola en vez
// de dejarla abierta indefinidamente (el refresh token por sí solo no
// vence hasta los 30 días, sin importar la inactividad).
@Injectable({ providedIn: 'root' })
export class InactividadService {
  mostrarAviso = signal(false);
  segundosParaCierre = signal(SEGUNDOS_AVISO);

  private timerAviso: ReturnType<typeof setTimeout> | null = null;
  private intervaloCountdown: ReturnType<typeof setInterval> | null = null;
  private activo = false;

  private readonly registrarActividad = (): void => {
    // Mientras el cartel de aviso está en pantalla, un movimiento de mouse
    // de fondo no cuenta como "seguir conectado" — hace falta la acción
    // explícita (botón "Seguir conectado") o cerrar el cartel de otra forma.
    if (this.mostrarAviso()) return;
    this.reiniciarTimers();
  };

  constructor(
    private auth: AuthService,
    private zone: NgZone
  ) {}

  iniciar(): void {
    if (this.activo) return;
    this.activo = true;

    this.zone.runOutsideAngular(() => {
      for (const evento of EVENTOS_ACTIVIDAD) {
        window.addEventListener(evento, this.registrarActividad, { passive: true });
      }
    });

    this.reiniciarTimers();
  }

  detener(): void {
    if (!this.activo) return;
    this.activo = false;

    for (const evento of EVENTOS_ACTIVIDAD) {
      window.removeEventListener(evento, this.registrarActividad);
    }
    this.limpiarTimers();
    this.mostrarAviso.set(false);
  }

  /// Lo llama el botón "Seguir conectado" del cartel de aviso.
  seguirConectado(): void {
    this.reiniciarTimers();
  }

  private reiniciarTimers(): void {
    this.limpiarTimers();
    this.mostrarAviso.set(false);

    const msHastaAviso = (MINUTOS_INACTIVIDAD * 60 - SEGUNDOS_AVISO) * 1000;
    this.timerAviso = setTimeout(() => this.zone.run(() => this.mostrarCartelAviso()), msHastaAviso);
  }

  private mostrarCartelAviso(): void {
    this.mostrarAviso.set(true);
    this.segundosParaCierre.set(SEGUNDOS_AVISO);

    this.intervaloCountdown = setInterval(() => {
      const restante = this.segundosParaCierre() - 1;
      this.segundosParaCierre.set(restante);
      if (restante <= 0) {
        this.limpiarTimers();
        this.mostrarAviso.set(false);
        this.auth.logout('inactividad');
      }
    }, 1000);
  }

  private limpiarTimers(): void {
    if (this.timerAviso) clearTimeout(this.timerAviso);
    if (this.intervaloCountdown) clearInterval(this.intervaloCountdown);
    this.timerAviso = null;
    this.intervaloCountdown = null;
  }
}
