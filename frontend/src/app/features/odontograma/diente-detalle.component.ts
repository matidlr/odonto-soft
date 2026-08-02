import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import {
  ArchivoOdontograma,
  EstadoDiente,
  EstadoTratamiento,
  EventoOdontograma,
  OdontogramaService
} from '../../core/odontograma.service';
import { Turno, TurnoService } from '../../core/turno.service';

const ESTADOS: EstadoDiente[] = [
  'Sano',
  'Cariado',
  'Obturado',
  'Corona',
  'Endodoncia',
  'Ausente',
  'Implante',
  'Fracturado',
  'Sellador',
  'Ortodoncia'
];

interface ArchivoAbierto {
  url: string;
  nombre: string;
  contentType: string;
  esPrevisualizable: boolean;
}

@Component({
  selector: 'app-diente-detalle',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './diente-detalle.component.html',
  styleUrl: './diente-detalle.component.scss'
})
export class DienteDetalleComponent implements OnInit, OnDestroy {
  estados = ESTADOS;

  pacienteId = '';
  numeroFdi = 0;

  odontologos = signal<Odontologo[]>([]);
  turnosPaciente = signal<Turno[]>([]);
  historial = signal<EventoOdontograma[]>([]);
  cargando = signal(true);

  guardando = signal(false);
  error = signal<string | null>(null);
  subiendoArchivoDe = signal<string | null>(null);

  // Miniaturas de imágenes ya descargadas, cacheadas por id de archivo
  // (para no volver a pedirlas cada vez que se re-renderiza la lista).
  miniaturas = signal<Map<string, string>>(new Map());
  archivoAbierto = signal<ArchivoAbierto | null>(null);

  nuevoEstado: EstadoDiente = 'Cariado';
  nuevoEstadoTratamiento: EstadoTratamiento = 'Realizado';
  nuevoTratamiento = '';
  nuevaNota = '';
  nuevoOdontologoId = '';

  // '' = sin turno asociado (fecha a mano); si no, es el id del turno elegido.
  turnoSeleccionado = '';
  fechaManual = '';

  constructor(
    private route: ActivatedRoute,
    private odontogramaService: OdontogramaService,
    private odontologoService: OdontologoService,
    private turnoService: TurnoService,
    private sanitizer: DomSanitizer
  ) {}

  // El <iframe> exige una URL "de confianza" explícita para poder mostrar
  // el blob del PDF; sin esto Angular la bloquea por seguridad.
  urlSegura(url: string): SafeResourceUrl {
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    this.numeroFdi = Number(this.route.snapshot.paramMap.get('numeroFdi'));
    this.fechaManual = new Date().toISOString().slice(0, 10);

    this.cargando.set(true);
    try {
      const [historial, odontologos, turnos] = await Promise.all([
        this.odontogramaService.getHistorial(this.pacienteId, this.numeroFdi),
        this.odontologoService.getAll(),
        this.turnoService.getAll(undefined, undefined, undefined, this.pacienteId)
      ]);
      this.historial.set(historial);
      this.odontologos.set(odontologos);
      this.turnosPaciente.set(turnos);
      await this.cargarMiniaturas(historial);
    } finally {
      this.cargando.set(false);
    }
  }

  ngOnDestroy(): void {
    for (const url of this.miniaturas().values()) {
      URL.revokeObjectURL(url);
    }
  }

  esImagen(archivo: ArchivoOdontograma): boolean {
    return archivo.contentType.startsWith('image/');
  }

  private async cargarMiniaturas(eventos: EventoOdontograma[]): Promise<void> {
    const mapa = new Map(this.miniaturas());
    for (const ev of eventos) {
      for (const archivo of ev.archivos) {
        if (this.esImagen(archivo) && !mapa.has(archivo.id)) {
          const blob = await this.odontogramaService.descargarArchivo(archivo.id);
          mapa.set(archivo.id, URL.createObjectURL(blob));
        }
      }
    }
    this.miniaturas.set(mapa);
  }

  async cargarHistorial(): Promise<void> {
    const historial = await this.odontogramaService.getHistorial(this.pacienteId, this.numeroFdi);
    this.historial.set(historial);
    await this.cargarMiniaturas(historial);
  }

  async registrarEvento(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.odontogramaService.crearEvento(this.pacienteId, {
        numeroFdi: this.numeroFdi,
        estado: this.nuevoEstado,
        estadoTratamiento: this.nuevoEstadoTratamiento,
        tratamiento: this.nuevoTratamiento || undefined,
        nota: this.nuevaNota || undefined,
        odontologoId: this.nuevoOdontologoId || undefined,
        turnoId: this.turnoSeleccionado || undefined,
        fecha: this.turnoSeleccionado
          ? undefined
          : new Date(this.fechaManual).toISOString()
      });
      this.nuevoTratamiento = '';
      this.nuevaNota = '';
      await this.cargarHistorial();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo registrar el evento.');
    } finally {
      this.guardando.set(false);
    }
  }

  async adjuntarArchivo(evento: EventoOdontograma, input: HTMLInputElement): Promise<void> {
    const archivo = input.files?.[0];
    if (!archivo) return;

    this.subiendoArchivoDe.set(evento.id);
    try {
      await this.odontogramaService.subirArchivo(evento.id, archivo);
      await this.cargarHistorial();
    } catch {
      this.error.set('No se pudo subir el archivo.');
    } finally {
      this.subiendoArchivoDe.set(null);
      input.value = '';
    }
  }

  // Abre el visor dentro de la página. Si es imagen o PDF se puede
  // previsualizar; cualquier otro tipo muestra el botón de descarga.
  async abrirVisor(archivo: ArchivoOdontograma): Promise<void> {
    const esPdf = archivo.contentType === 'application/pdf';
    const esImagen = this.esImagen(archivo);

    const urlCacheada = this.miniaturas().get(archivo.id);
    const url = urlCacheada ?? URL.createObjectURL(await this.odontogramaService.descargarArchivo(archivo.id));

    this.archivoAbierto.set({
      url,
      nombre: archivo.nombreOriginal,
      contentType: archivo.contentType,
      esPrevisualizable: esPdf || esImagen
    });
  }

  cerrarVisor(): void {
    this.archivoAbierto.set(null);
  }

  descargarDesdeVisor(): void {
    const abierto = this.archivoAbierto();
    if (!abierto) return;
    const a = document.createElement('a');
    a.href = abierto.url;
    a.download = abierto.nombre;
    a.click();
  }
}
