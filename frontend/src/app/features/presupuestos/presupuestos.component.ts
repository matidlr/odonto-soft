import { DatePipe, CurrencyPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EstadoDiente } from '../../core/odontograma.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Paciente, PacienteService } from '../../core/paciente.service';
import {
  CrearPresupuestoRequest,
  ItemPresupuestoRequest,
  Presupuesto,
  PresupuestoService
} from '../../core/presupuesto.service';
import { TipoTratamiento, TipoTratamientoService } from '../../core/tipo-tratamiento.service';

interface FilaItem {
  tipoTratamientoId: string;
  descripcion: string;
  numeroFdi: string;
  estadoDienteResultante: EstadoDiente | '';
  cantidad: number;
  precioUnitario: number;
}

const ESTADOS_DIENTE: EstadoDiente[] = [
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

function filaVacia(): FilaItem {
  return { tipoTratamientoId: '', descripcion: '', numeroFdi: '', estadoDienteResultante: '', cantidad: 1, precioUnitario: 0 };
}

@Component({
  selector: 'app-presupuestos',
  standalone: true,
  imports: [FormsModule, DatePipe, CurrencyPipe, RouterLink],
  templateUrl: './presupuestos.component.html',
  styleUrl: './presupuestos.component.scss'
})
export class PresupuestosComponent implements OnInit {
  estadosDiente = ESTADOS_DIENTE;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  presupuestos = signal<Presupuesto[]>([]);
  odontologos = signal<Odontologo[]>([]);
  tiposTratamiento = signal<TipoTratamiento[]>([]);
  cargando = signal(true);

  mostrarForm = signal(false);
  odontologoId = '';
  observaciones = '';
  filas = signal<FilaItem[]>([filaVacia()]);
  guardando = signal(false);
  error = signal<string | null>(null);

  procesandoId = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private presupuestoService: PresupuestoService,
    private pacienteService: PacienteService,
    private odontologoService: OdontologoService,
    private tipoTratamientoService: TipoTratamientoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    await this.cargarTodo();
  }

  async cargarTodo(): Promise<void> {
    this.cargando.set(true);
    try {
      const [presupuestos, pacientes, odontologos, tipos] = await Promise.all([
        this.presupuestoService.getPorPaciente(this.pacienteId),
        this.pacienteService.getAll(),
        this.odontologoService.getAll(),
        this.tipoTratamientoService.getAll()
      ]);
      this.presupuestos.set(presupuestos);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
      this.odontologos.set(odontologos);
      this.tiposTratamiento.set(tipos);
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.odontologoId = '';
    this.observaciones = '';
    this.filas.set([filaVacia()]);
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
  }

  agregarFila(): void {
    this.filas.set([...this.filas(), filaVacia()]);
  }

  quitarFila(index: number): void {
    this.filas.set(this.filas().filter((_, i) => i !== index));
  }

  onCambioTipoTratamiento(index: number): void {
    const filas = [...this.filas()];
    const fila = filas[index];
    const tipo = this.tiposTratamiento().find((t) => t.id === fila.tipoTratamientoId);
    if (tipo) {
      fila.descripcion = fila.descripcion || tipo.nombre;
      fila.precioUnitario = fila.precioUnitario || tipo.precioBase;
    }
    this.filas.set(filas);
  }

  totalFilas(): number {
    return this.filas().reduce((acc, f) => acc + f.cantidad * f.precioUnitario, 0);
  }

  async guardar(): Promise<void> {
    this.error.set(null);

    const items: ItemPresupuestoRequest[] = [];
    for (const fila of this.filas()) {
      if (!fila.descripcion.trim()) continue;
      items.push({
        tipoTratamientoId: fila.tipoTratamientoId || undefined,
        descripcion: fila.descripcion,
        numeroFdi: fila.numeroFdi ? Number(fila.numeroFdi) : undefined,
        estadoDienteResultante: fila.estadoDienteResultante || undefined,
        cantidad: fila.cantidad,
        precioUnitario: fila.precioUnitario
      });
    }

    if (items.length === 0) {
      this.error.set('Agregá al menos un ítem con descripción.');
      return;
    }

    const datos: CrearPresupuestoRequest = {
      odontologoId: this.odontologoId || undefined,
      observaciones: this.observaciones || undefined,
      items
    };

    this.guardando.set(true);
    try {
      await this.presupuestoService.crear(this.pacienteId, datos);
      this.mostrarForm.set(false);
      this.presupuestos.set(await this.presupuestoService.getPorPaciente(this.pacienteId));
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar el presupuesto.');
    } finally {
      this.guardando.set(false);
    }
  }

  async aprobar(p: Presupuesto): Promise<void> {
    await this.cambiarEstado(p, 'Aprobado');
  }

  async rechazar(p: Presupuesto): Promise<void> {
    await this.cambiarEstado(p, 'Rechazado');
  }

  private async cambiarEstado(p: Presupuesto, estado: 'Aprobado' | 'Rechazado'): Promise<void> {
    this.procesandoId.set(p.id);
    try {
      await this.presupuestoService.cambiarEstado(p.id, estado);
      this.presupuestos.set(await this.presupuestoService.getPorPaciente(this.pacienteId));
    } finally {
      this.procesandoId.set(null);
    }
  }

  async convertir(p: Presupuesto): Promise<void> {
    if (!confirm('¿Convertir este presupuesto en tratamiento? Los ítems con diente asignado van a aparecer como planificados en el odontograma.')) return;
    this.procesandoId.set(p.id);
    try {
      await this.presupuestoService.convertir(p.id);
      this.presupuestos.set(await this.presupuestoService.getPorPaciente(this.pacienteId));
    } finally {
      this.procesandoId.set(null);
    }
  }

  async borrar(p: Presupuesto): Promise<void> {
    if (!confirm('¿Borrar este presupuesto pendiente?')) return;
    this.procesandoId.set(p.id);
    try {
      await this.presupuestoService.borrar(p.id);
      this.presupuestos.set(this.presupuestos().filter((x) => x.id !== p.id));
    } finally {
      this.procesandoId.set(null);
    }
  }

  nombreOdontologo(id: string | null): string {
    if (!id) return '';
    return this.odontologos().find((o) => o.id === id)?.nombre ?? '';
  }
}
