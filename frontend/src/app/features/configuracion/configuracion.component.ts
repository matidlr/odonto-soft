import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService, Sesion } from '../../core/auth.service';
import { Configuracion, ConfiguracionService } from '../../core/configuracion.service';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe],
  templateUrl: './configuracion.component.html',
  styleUrl: './configuracion.component.scss'
})
export class ConfiguracionComponent implements OnInit {
  cargando = signal(true);
  guardando = signal(false);
  error = signal<string | null>(null);
  guardado = signal(false);

  nombre = '';
  direccion = '';
  telefono = '';
  emailContacto = '';
  tieneLogo = signal(false);
  logoUrl = signal<string | null>(null);

  subiendoLogo = signal(false);
  errorLogo = signal<string | null>(null);

  cerrandoTodos = signal(false);
  mensajeCerrarTodos = signal<string | null>(null);
  errorCerrarTodos = signal<string | null>(null);

  sesiones = signal<Sesion[]>([]);
  cargandoSesiones = signal(true);
  cerrandoSesionId = signal<string | null>(null);
  errorSesiones = signal<string | null>(null);

  constructor(
    private configuracionService: ConfiguracionService,
    private authService: AuthService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([this.cargar(), this.cargarSesiones()]);
  }

  async cargarSesiones(): Promise<void> {
    this.cargandoSesiones.set(true);
    try {
      this.sesiones.set(await this.authService.getSesiones());
    } catch {
      this.errorSesiones.set('No se pudieron cargar las sesiones activas.');
    } finally {
      this.cargandoSesiones.set(false);
    }
  }

  descripcionDispositivo(userAgent: string | null): string {
    if (!userAgent) return 'Dispositivo desconocido';
    // No hace falta un parser completo de User-Agent para esto — alcanza
    // con reconocer el navegador y el sistema operativo más comunes.
    const navegador = /Edg\//.test(userAgent)
      ? 'Edge'
      : /Chrome\//.test(userAgent)
        ? 'Chrome'
        : /Firefox\//.test(userAgent)
          ? 'Firefox'
          : /Safari\//.test(userAgent)
            ? 'Safari'
            : 'Navegador desconocido';
    const so = /Windows/.test(userAgent)
      ? 'Windows'
      : /Mac OS/.test(userAgent)
        ? 'macOS'
        : /Android/.test(userAgent)
          ? 'Android'
          : /iPhone|iPad/.test(userAgent)
            ? 'iOS'
            : /Linux/.test(userAgent)
              ? 'Linux'
              : '';
    return so ? `${navegador} en ${so}` : navegador;
  }

  async cerrarSesion(sesion: Sesion): Promise<void> {
    const texto = sesion.esActual
      ? '¿Cerrar esta sesión? Es la que estás usando ahora, así que vas a tener que volver a iniciar sesión.'
      : '¿Cerrar esta sesión?';
    if (!confirm(texto)) return;

    this.cerrandoSesionId.set(sesion.id);
    try {
      await this.authService.cerrarSesion(sesion.id, sesion.esActual);
      if (!sesion.esActual) {
        this.sesiones.set(this.sesiones().filter((s) => s.id !== sesion.id));
      }
    } catch {
      this.errorSesiones.set('No se pudo cerrar esa sesión.');
    } finally {
      this.cerrandoSesionId.set(null);
    }
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      const config = await this.configuracionService.get();
      this.aplicar(config);
      if (config.tieneLogo) {
        await this.cargarLogo();
      }
    } finally {
      this.cargando.set(false);
    }
  }

  private aplicar(config: Configuracion): void {
    this.nombre = config.nombre;
    this.direccion = config.direccion ?? '';
    this.telefono = config.telefono ?? '';
    this.emailContacto = config.emailContacto ?? '';
    this.tieneLogo.set(config.tieneLogo);
  }

  private async cargarLogo(): Promise<void> {
    try {
      const blob = await this.configuracionService.getLogoBlob();
      this.logoUrl.set(URL.createObjectURL(blob));
    } catch {
      this.logoUrl.set(null);
    }
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    this.guardado.set(false);
    this.guardando.set(true);
    try {
      const config = await this.configuracionService.editar({
        nombre: this.nombre,
        direccion: this.direccion || undefined,
        telefono: this.telefono || undefined,
        emailContacto: this.emailContacto || undefined
      });
      this.aplicar(config);
      this.guardado.set(true);
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar la configuración.');
    } finally {
      this.guardando.set(false);
    }
  }

  async onSeleccionarLogo(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const archivo = input.files?.[0];
    if (!archivo) return;

    this.errorLogo.set(null);
    this.subiendoLogo.set(true);
    try {
      await this.configuracionService.subirLogo(archivo);
      this.tieneLogo.set(true);
      await this.cargarLogo();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorLogo.set(httpError?.error?.message ?? 'No se pudo subir el logo.');
    } finally {
      this.subiendoLogo.set(false);
      input.value = '';
    }
  }

  async cerrarSesionEnTodosLados(): Promise<void> {
    if (!confirm('¿Cerrar sesión en todos los dispositivos donde iniciaste sesión?')) return;

    this.errorCerrarTodos.set(null);
    this.mensajeCerrarTodos.set(null);
    this.cerrandoTodos.set(true);
    try {
      // Redirige al login solo, así que no hace falta hacer nada más acá.
      await this.authService.logoutTodos();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorCerrarTodos.set(httpError?.error?.message ?? 'No se pudo cerrar la sesión en todos los dispositivos.');
      this.cerrandoTodos.set(false);
    }
  }
}
