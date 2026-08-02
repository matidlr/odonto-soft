import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  FichaMedica,
  HistorialClinicoService,
  NotaEvolucion
} from '../../core/historial-clinico.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Paciente, PacienteService } from '../../core/paciente.service';
import { Turno, TurnoService } from '../../core/turno.service';

@Component({
  selector: 'app-historial-clinico',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './historial-clinico.component.html',
  styleUrl: './historial-clinico.component.scss'
})
export class HistorialClinicoComponent implements OnInit {
  pacienteId = '';
  paciente = signal<Paciente | null>(null);

  cargando = signal(true);

  // Ficha médica
  alergias = '';
  enfermedadesPreexistentes = '';
  medicacionActual = '';
  habitos = '';
  observaciones = '';
  fechaActualizacionFicha = signal<string | null>(null);
  guardandoFicha = signal(false);
  errorFicha = signal<string | null>(null);

  // Notas de evolución
  notas = signal<NotaEvolucion[]>([]);
  odontologos = signal<Odontologo[]>([]);
  turnosPaciente = signal<Turno[]>([]);

  nuevoMotivo = '';
  nuevoDiagnostico = '';
  nuevoTratamientoRealizado = '';
  nuevaEvolucion = '';
  nuevaMedicacion = '';
  nuevasObservaciones = '';
  nuevoOdontologoId = '';
  turnoSeleccionado = '';
  fechaManual = '';
  guardandoNota = signal(false);
  errorNota = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private historialService: HistorialClinicoService,
    private pacienteService: PacienteService,
    private odontologoService: OdontologoService,
    private turnoService: TurnoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    this.fechaManual = new Date().toISOString().slice(0, 10);

    this.cargando.set(true);
    try {
      const [pacientes, ficha, notas, odontologos, turnos] = await Promise.all([
        this.pacienteService.getAll(),
        this.historialService.getFichaMedica(this.pacienteId),
        this.historialService.getNotasEvolucion(this.pacienteId),
        this.odontologoService.getAll(),
        this.turnoService.getAll(undefined, undefined, undefined, this.pacienteId)
      ]);

      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
      this.cargarFichaEnFormulario(ficha);
      this.notas.set(notas);
      this.odontologos.set(odontologos);
      this.turnosPaciente.set(turnos);
    } finally {
      this.cargando.set(false);
    }
  }

  private cargarFichaEnFormulario(ficha: FichaMedica): void {
    this.alergias = ficha.alergias ?? '';
    this.enfermedadesPreexistentes = ficha.enfermedadesPreexistentes ?? '';
    this.medicacionActual = ficha.medicacionActual ?? '';
    this.habitos = ficha.habitos ?? '';
    this.observaciones = ficha.observaciones ?? '';
    this.fechaActualizacionFicha.set(ficha.fechaActualizacion);
  }

  async guardarFicha(): Promise<void> {
    this.errorFicha.set(null);
    this.guardandoFicha.set(true);
    try {
      await this.historialService.guardarFichaMedica(this.pacienteId, {
        alergias: this.alergias || undefined,
        enfermedadesPreexistentes: this.enfermedadesPreexistentes || undefined,
        medicacionActual: this.medicacionActual || undefined,
        habitos: this.habitos || undefined,
        observaciones: this.observaciones || undefined
      });
      const ficha = await this.historialService.getFichaMedica(this.pacienteId);
      this.cargarFichaEnFormulario(ficha);
    } catch {
      this.errorFicha.set('No se pudo guardar la ficha médica.');
    } finally {
      this.guardandoFicha.set(false);
    }
  }

  async agregarNota(): Promise<void> {
    this.errorNota.set(null);
    this.guardandoNota.set(true);
    try {
      await this.historialService.crearNotaEvolucion(this.pacienteId, {
        motivo: this.nuevoMotivo || undefined,
        diagnostico: this.nuevoDiagnostico || undefined,
        tratamientoRealizado: this.nuevoTratamientoRealizado || undefined,
        evolucion: this.nuevaEvolucion || undefined,
        medicacion: this.nuevaMedicacion || undefined,
        observaciones: this.nuevasObservaciones || undefined,
        odontologoId: this.nuevoOdontologoId || undefined,
        turnoId: this.turnoSeleccionado || undefined,
        fecha: this.turnoSeleccionado ? undefined : new Date(this.fechaManual).toISOString()
      });
      this.nuevoMotivo = '';
      this.nuevoDiagnostico = '';
      this.nuevoTratamientoRealizado = '';
      this.nuevaEvolucion = '';
      this.nuevaMedicacion = '';
      this.nuevasObservaciones = '';
      this.notas.set(await this.historialService.getNotasEvolucion(this.pacienteId));
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorNota.set(httpError?.error?.message ?? 'No se pudo agregar la nota.');
    } finally {
      this.guardandoNota.set(false);
    }
  }

  nombreOdontologo(id: string | null): string {
    if (!id) return '';
    return this.odontologos().find((o) => o.id === id)?.nombre ?? '';
  }
}
