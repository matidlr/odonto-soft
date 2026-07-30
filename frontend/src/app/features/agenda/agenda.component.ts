import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Paciente, PacienteService } from '../../core/paciente.service';
import { TipoTratamiento, TipoTratamientoService } from '../../core/tipo-tratamiento.service';
import { Turno, TurnoEstado, TurnoService } from '../../core/turno.service';

const ESTADOS: TurnoEstado[] = ['Solicitado', 'Confirmado', 'Cancelado', 'Completado', 'Ausente'];

function inicioDeHoy(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}

function formatoInputDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [FormsModule, DatePipe],
  templateUrl: './agenda.component.html',
  styleUrl: './agenda.component.scss'
})
export class AgendaComponent implements OnInit {
  estados = ESTADOS;

  turnos = signal<Turno[]>([]);
  odontologos = signal<Odontologo[]>([]);
  pacientes = signal<Paciente[]>([]);
  tiposTratamiento = signal<TipoTratamiento[]>([]);

  cargando = signal(true);
  mostrarForm = signal(false);
  guardando = signal(false);
  error = signal<string | null>(null);
  errorForm = signal<string | null>(null);

  desde = formatoInputDate(inicioDeHoy());
  hasta = formatoInputDate(new Date(inicioDeHoy().getTime() + 7 * 24 * 60 * 60 * 1000));

  // Formulario de alta manual
  nuevoPacienteId = '';
  nuevoOdontologoId = '';
  nuevoTipoTratamientoId = '';
  nuevaFechaHora = '';

  nombreOdontologo = computed(() => {
    const mapa = new Map(this.odontologos().map((o) => [o.id, o.nombre]));
    return (id: string) => mapa.get(id) ?? '(desconocido)';
  });

  nombreTratamiento = computed(() => {
    const mapa = new Map(this.tiposTratamiento().map((t) => [t.id, t.nombre]));
    return (id: string | null) => (id ? mapa.get(id) ?? '(desconocido)' : '-');
  });

  constructor(
    private turnoService: TurnoService,
    private odontologoService: OdontologoService,
    private pacienteService: PacienteService,
    private tipoTratamientoService: TipoTratamientoService
  ) {}

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.cargarTurnos(),
      this.odontologoService.getAll().then((v) => this.odontologos.set(v)),
      this.pacienteService.getAll().then((v) => this.pacientes.set(v)),
      this.tipoTratamientoService.getAll().then((v) => this.tiposTratamiento.set(v))
    ]);
  }

  async cargarTurnos(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    try {
      const desde = new Date(this.desde);
      const hasta = new Date(this.hasta);
      hasta.setHours(23, 59, 59, 999);
      this.turnos.set(await this.turnoService.getAll(desde, hasta));
    } catch {
      this.error.set('No se pudieron cargar los turnos.');
    } finally {
      this.cargando.set(false);
    }
  }

  async cambiarEstado(turno: Turno, estado: TurnoEstado): Promise<void> {
    const anterior = turno.estado;
    turno.estado = estado; // optimista, así la UI responde al toque
    try {
      await this.turnoService.cambiarEstado(turno.id, estado);
    } catch {
      turno.estado = anterior;
      this.error.set('No se pudo cambiar el estado del turno.');
    }
  }

  async crearTurno(): Promise<void> {
    this.errorForm.set(null);
    this.guardando.set(true);
    try {
      await this.turnoService.crear({
        pacienteId: this.nuevoPacienteId,
        odontologoId: this.nuevoOdontologoId,
        tipoTratamientoId: this.nuevoTipoTratamientoId || undefined,
        fechaHora: new Date(this.nuevaFechaHora).toISOString()
      });
      this.nuevoPacienteId = this.nuevoOdontologoId = this.nuevoTipoTratamientoId = this.nuevaFechaHora = '';
      this.mostrarForm.set(false);
      await this.cargarTurnos();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorForm.set(httpError?.error?.message ?? 'No se pudo crear el turno.');
    } finally {
      this.guardando.set(false);
    }
  }
}
