import { DatePipe } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Consentimiento,
  ConsentimientoService,
  TipoConsentimiento
} from '../../core/consentimiento.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

const TIPOS: TipoConsentimiento[] = ['ConsentimientoInformado', 'Cirugia', 'Implante', 'Otro'];

const ETIQUETA_TIPO: Record<TipoConsentimiento, string> = {
  ConsentimientoInformado: 'Consentimiento informado (general)',
  Cirugia: 'Cirugía',
  Implante: 'Implante',
  Otro: 'Otro'
};

const PLANTILLA_TITULO: Record<TipoConsentimiento, string> = {
  ConsentimientoInformado: 'Consentimiento informado',
  Cirugia: 'Consentimiento informado para cirugía',
  Implante: 'Consentimiento informado para colocación de implante',
  Otro: ''
};

const PLANTILLA_TEXTO: Record<TipoConsentimiento, string> = {
  ConsentimientoInformado:
    'Declaro que el/la profesional me explicó el diagnóstico, el tratamiento propuesto, sus alternativas, riesgos y beneficios, y que pude hacer las preguntas que consideré necesarias. Habiendo comprendido esta información, presto mi consentimiento para la realización del tratamiento odontológico propuesto.',
  Cirugia:
    'Declaro que fui informado/a sobre el procedimiento quirúrgico a realizar, sus riesgos (incluyendo sangrado, infección, dolor postoperatorio y complicaciones anestésicas), beneficios esperados y alternativas de tratamiento. Habiendo comprendido esta información y evacuado mis dudas, autorizo la realización de la cirugía propuesta.',
  Implante:
    'Declaro que fui informado/a sobre el procedimiento de colocación de implante dental, incluyendo el proceso de osteointegración, los riesgos (rechazo, infección, daño a estructuras vecinas) y los cuidados postoperatorios necesarios. Habiendo comprendido esta información, autorizo la colocación del/los implante(s) propuesto(s).',
  Otro: ''
};

@Component({
  selector: 'app-consentimientos',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './consentimientos.component.html',
  styleUrl: './consentimientos.component.scss'
})
export class ConsentimientosComponent implements OnInit, AfterViewInit {
  tipos = TIPOS;
  etiquetaTipo = ETIQUETA_TIPO;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  consentimientos = signal<Consentimiento[]>([]);
  odontologos = signal<Odontologo[]>([]);
  cargando = signal(true);

  mostrarForm = signal(false);
  tipo: TipoConsentimiento = 'ConsentimientoInformado';
  titulo = '';
  texto = '';
  odontologoId = '';
  nombreAclaratorio = '';
  error = signal<string | null>(null);
  guardando = signal(false);

  // Firma que se está capturando para el consentimiento NUEVO (form de alta).
  firmaVacia = signal(true);

  // Consentimiento existente (sin firmar) que se está por firmar ahora.
  firmando = signal<Consentimiento | null>(null);
  firmaExistenteVacia = signal(true);
  errorFirma = signal<string | null>(null);
  guardandoFirma = signal(false);
  nombreAclaratorioExistente = '';

  verDetalle = signal<Consentimiento | null>(null);

  @ViewChild('canvasNuevo') canvasNuevoRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('canvasExistente') canvasExistenteRef?: ElementRef<HTMLCanvasElement>;

  private dibujando = false;
  private ultimoPunto: { x: number; y: number } | null = null;

  constructor(
    private route: ActivatedRoute,
    private consentimientoService: ConsentimientoService,
    private pacienteService: PacienteService,
    private odontologoService: OdontologoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    await this.cargarTodo();
  }

  ngAfterViewInit(): void {}

  async cargarTodo(): Promise<void> {
    this.cargando.set(true);
    try {
      const [consentimientos, pacientes, odontologos] = await Promise.all([
        this.consentimientoService.getPorPaciente(this.pacienteId),
        this.pacienteService.getAll(),
        this.odontologoService.getAll()
      ]);
      this.consentimientos.set(consentimientos);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
      this.odontologos.set(odontologos);
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.tipo = 'ConsentimientoInformado';
    this.aplicarPlantilla();
    this.odontologoId = '';
    this.nombreAclaratorio = '';
    this.error.set(null);
    this.mostrarForm.set(true);
    setTimeout(() => this.prepararCanvas(this.canvasNuevoRef), 0);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
  }

  aplicarPlantilla(): void {
    this.titulo = PLANTILLA_TITULO[this.tipo];
    this.texto = PLANTILLA_TEXTO[this.tipo];
  }

  private prepararCanvas(ref: ElementRef<HTMLCanvasElement> | undefined): void {
    const canvas = ref?.nativeElement;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.strokeStyle = '#111827';
  }

  private coordenadas(canvas: HTMLCanvasElement, evento: PointerEvent): { x: number; y: number } {
    const rect = canvas.getBoundingClientRect();
    return { x: evento.clientX - rect.left, y: evento.clientY - rect.top };
  }

  iniciarTrazo(evento: PointerEvent, cual: 'nuevo' | 'existente'): void {
    const ref = cual === 'nuevo' ? this.canvasNuevoRef : this.canvasExistenteRef;
    const canvas = ref?.nativeElement;
    if (!canvas) return;
    this.dibujando = true;
    this.ultimoPunto = this.coordenadas(canvas, evento);
  }

  continuarTrazo(evento: PointerEvent, cual: 'nuevo' | 'existente'): void {
    if (!this.dibujando) return;
    const ref = cual === 'nuevo' ? this.canvasNuevoRef : this.canvasExistenteRef;
    const canvas = ref?.nativeElement;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx || !this.ultimoPunto) return;

    const punto = this.coordenadas(canvas, evento);
    ctx.beginPath();
    ctx.moveTo(this.ultimoPunto.x, this.ultimoPunto.y);
    ctx.lineTo(punto.x, punto.y);
    ctx.stroke();
    this.ultimoPunto = punto;

    if (cual === 'nuevo') this.firmaVacia.set(false);
    else this.firmaExistenteVacia.set(false);
  }

  finalizarTrazo(): void {
    this.dibujando = false;
    this.ultimoPunto = null;
  }

  limpiarFirma(cual: 'nuevo' | 'existente'): void {
    const ref = cual === 'nuevo' ? this.canvasNuevoRef : this.canvasExistenteRef;
    this.prepararCanvas(ref);
    if (cual === 'nuevo') this.firmaVacia.set(true);
    else this.firmaExistenteVacia.set(true);
  }

  async guardar(firmarAhora: boolean): Promise<void> {
    this.error.set(null);
    if (!this.titulo.trim() || !this.texto.trim()) {
      this.error.set('Completá el título y el texto del consentimiento.');
      return;
    }

    this.guardando.set(true);
    try {
      const firma = firmarAhora && !this.firmaVacia() ? this.canvasNuevoRef?.nativeElement.toDataURL('image/png') : undefined;

      await this.consentimientoService.crear(this.pacienteId, {
        tipo: this.tipo,
        titulo: this.titulo,
        texto: this.texto,
        odontologoId: this.odontologoId || undefined,
        firmaBase64: firma,
        firmaNombreAclaratorio: firma ? this.nombreAclaratorio || undefined : undefined
      });

      this.mostrarForm.set(false);
      await this.cargarTodo();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar el consentimiento.');
    } finally {
      this.guardando.set(false);
    }
  }

  abrirFirma(c: Consentimiento): void {
    this.firmando.set(c);
    this.nombreAclaratorioExistente = '';
    this.errorFirma.set(null);
    setTimeout(() => this.prepararCanvas(this.canvasExistenteRef), 0);
  }

  cerrarFirma(): void {
    this.firmando.set(null);
  }

  async confirmarFirma(): Promise<void> {
    const c = this.firmando();
    if (!c || this.firmaExistenteVacia()) {
      this.errorFirma.set('Falta dibujar la firma.');
      return;
    }

    this.errorFirma.set(null);
    this.guardandoFirma.set(true);
    try {
      const firma = this.canvasExistenteRef?.nativeElement.toDataURL('image/png');
      if (!firma) return;

      await this.consentimientoService.firmar(c.id, {
        firmaBase64: firma,
        firmaNombreAclaratorio: this.nombreAclaratorioExistente || undefined
      });

      this.firmando.set(null);
      await this.cargarTodo();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorFirma.set(httpError?.error?.message ?? 'No se pudo guardar la firma.');
    } finally {
      this.guardandoFirma.set(false);
    }
  }

  async borrar(c: Consentimiento): Promise<void> {
    if (!confirm('¿Borrar este consentimiento sin firmar?')) return;
    await this.consentimientoService.borrar(c.id);
    await this.cargarTodo();
  }

  abrirDetalle(c: Consentimiento): void {
    this.verDetalle.set(c);
  }

  cerrarDetalle(): void {
    this.verDetalle.set(null);
  }

  nombreOdontologo(id: string | null): string {
    if (!id) return '';
    return this.odontologos().find((o) => o.id === id)?.nombre ?? '';
  }
}
