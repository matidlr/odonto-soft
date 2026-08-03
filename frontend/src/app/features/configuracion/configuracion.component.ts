import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Configuracion, ConfiguracionService } from '../../core/configuracion.service';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [FormsModule, RouterLink],
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

  constructor(private configuracionService: ConfiguracionService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
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
}
