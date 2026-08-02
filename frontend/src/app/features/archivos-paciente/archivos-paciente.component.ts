import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  ArchivoPaciente,
  ArchivoPacienteService,
  CategoriaArchivo
} from '../../core/archivo-paciente.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

const CATEGORIAS: CategoriaArchivo[] = ['Radiografia', 'Foto', 'Estudio', 'Documento'];

const ETIQUETA_CATEGORIA: Record<CategoriaArchivo, string> = {
  Radiografia: 'Radiografía',
  Foto: 'Foto',
  Estudio: 'Estudio',
  Documento: 'PDF / Documento'
};

interface ArchivoAbierto {
  url: string;
  nombre: string;
  contentType: string;
  esPrevisualizable: boolean;
}

@Component({
  selector: 'app-archivos-paciente',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './archivos-paciente.component.html',
  styleUrl: './archivos-paciente.component.scss'
})
export class ArchivosPacienteComponent implements OnInit {
  categorias = CATEGORIAS;
  etiquetaCategoria = ETIQUETA_CATEGORIA;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  archivos = signal<ArchivoPaciente[]>([]);
  cargando = signal(true);

  filtroCategoria = signal<CategoriaArchivo | 'Todas'>('Todas');
  archivosFiltrados = computed(() => {
    const filtro = this.filtroCategoria();
    const lista = this.archivos();
    return filtro === 'Todas' ? lista : lista.filter((a) => a.categoria === filtro);
  });

  archivoSeleccionado: File | null = null;
  categoriaNueva: CategoriaArchivo = 'Radiografia';
  descripcionNueva = '';
  subiendo = signal(false);
  error = signal<string | null>(null);

  archivoAbierto = signal<ArchivoAbierto | null>(null);
  borrandoId = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private archivoService: ArchivoPacienteService,
    private pacienteService: PacienteService,
    private sanitizer: DomSanitizer
  ) {}

  // El <iframe> exige una URL "de confianza" explícita para poder mostrar
  // el blob del PDF; sin esto Angular la bloquea por seguridad.
  urlSegura(url: string): SafeResourceUrl {
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    await this.cargarTodo();
  }

  async cargarTodo(): Promise<void> {
    this.cargando.set(true);
    try {
      const [archivos, pacientes] = await Promise.all([
        this.archivoService.getArchivos(this.pacienteId),
        this.pacienteService.getAll()
      ]);
      this.archivos.set(archivos);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
    } finally {
      this.cargando.set(false);
    }
  }

  onSeleccionarArchivo(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.archivoSeleccionado = input.files?.[0] ?? null;
  }

  async subir(): Promise<void> {
    if (!this.archivoSeleccionado) return;
    this.error.set(null);
    this.subiendo.set(true);
    try {
      await this.archivoService.subirArchivo(
        this.pacienteId,
        this.archivoSeleccionado,
        this.categoriaNueva,
        this.descripcionNueva || undefined
      );
      this.archivoSeleccionado = null;
      this.descripcionNueva = '';
      const input = document.getElementById('inputArchivo') as HTMLInputElement | null;
      if (input) input.value = '';
      this.archivos.set(await this.archivoService.getArchivos(this.pacienteId));
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo subir el archivo.');
    } finally {
      this.subiendo.set(false);
    }
  }

  esImagen(archivo: ArchivoPaciente): boolean {
    return archivo.contentType.startsWith('image/');
  }

  async abrirVisor(archivo: ArchivoPaciente): Promise<void> {
    const esPdf = archivo.contentType === 'application/pdf';
    const esImagen = this.esImagen(archivo);
    const blob = await this.archivoService.descargarArchivo(this.pacienteId, archivo.id);
    const url = URL.createObjectURL(blob);

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

  async borrar(archivo: ArchivoPaciente): Promise<void> {
    if (!confirm(`¿Borrar "${archivo.nombreOriginal}"?`)) return;
    this.borrandoId.set(archivo.id);
    try {
      await this.archivoService.borrarArchivo(this.pacienteId, archivo.id);
      this.archivos.set(this.archivos().filter((a) => a.id !== archivo.id));
    } finally {
      this.borrandoId.set(null);
    }
  }

  formatearTamanio(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
