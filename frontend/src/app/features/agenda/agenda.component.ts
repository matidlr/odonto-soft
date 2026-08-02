import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Paciente, PacienteService } from '../../core/paciente.service';
import { DisponibilidadService } from '../../core/disponibilidad.service';
import { TipoTratamiento, TipoTratamientoService } from '../../core/tipo-tratamiento.service';
import { DiaAgenda, Turno, TurnoDelDia, TurnoEstado, TurnoService } from '../../core/turno.service';

const ESTADOS: TurnoEstado[] = ['Solicitado', 'Confirmado', 'Cancelado', 'Completado', 'Ausente'];
const PASO_MINUTOS = 30;

export interface SlotDia {
  hora: string; // "HH:mm"
  estado: 'libre' | 'reservado' | 'bloqueado';
  turno?: TurnoDelDia;
}

function inicioDeMes(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}

function finDeMes(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59, 999);
}

function claveFecha(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function parseHora(s: string): number {
  const [h, m] = s.split(':').map(Number);
  return h * 60 + m;
}

function formatHora(mins: number): string {
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [FormsModule, DatePipe, RouterLink],
  templateUrl: './agenda.component.html',
  styleUrl: './agenda.component.scss'
})
export class AgendaComponent implements OnInit {
  estados = ESTADOS;

  mesActual = signal(inicioDeMes(new Date()));
  diaSeleccionado = signal<Date | null>(null);

  turnosDelMes = signal<Turno[]>([]);
  cargandoMes = signal(false);

  diaDetalle = signal<DiaAgenda | null>(null);
  cargandoDia = signal(false);

  pacientes = signal<Paciente[]>([]);
  tiposTratamiento = signal<TipoTratamiento[]>([]);

  modoBloqueo = signal(false);
  procesandoBloqueo = signal(false);

  // Formulario de reserva (se abre al hacer clic en una franja libre)
  slotParaReservar = signal<string | null>(null);
  nuevoPacienteId = '';
  nuevoTipoTratamientoId = '';
  horaHasta = '';
  guardandoTurno = signal(false);
  errorTurno = signal<string | null>(null);

  // Detalle de un turno ya reservado (al hacer clic en una franja ocupada)
  turnoSeleccionado = signal<TurnoDelDia | null>(null);
  cambiandoEstado = signal(false);

  error = signal<string | null>(null);

  nombreMes = computed(() => {
    const opciones: Intl.DateTimeFormatOptions = { month: 'long', year: 'numeric' };
    return this.mesActual().toLocaleDateString('es-AR', opciones);
  });

  diasDelMes = computed<(Date | null)[]>(() => {
    const primerDia = this.mesActual();
    const year = primerDia.getFullYear();
    const month = primerDia.getMonth();
    const diasEnMes = new Date(year, month + 1, 0).getDate();
    // getDay(): 0=domingo..6=sábado → lo corremos para que la semana arranque lunes.
    const primerDiaSemana = (primerDia.getDay() + 6) % 7;

    const celdas: (Date | null)[] = [];
    for (let i = 0; i < primerDiaSemana; i++) celdas.push(null);
    for (let d = 1; d <= diasEnMes; d++) celdas.push(new Date(year, month, d));
    return celdas;
  });

  cantidadPorDia = computed(() => {
    const mapa = new Map<string, number>();
    for (const t of this.turnosDelMes()) {
      if (t.estado === 'Cancelado') continue;
      const key = t.fechaHora.slice(0, 10);
      mapa.set(key, (mapa.get(key) ?? 0) + 1);
    }
    return mapa;
  });

  slotsDelDia = computed<SlotDia[]>(() => {
    const dia = this.diaDetalle();
    if (!dia || dia.ventanas.length === 0) return [];

    const minInicio = Math.min(...dia.ventanas.map((v) => parseHora(v.horaInicio)));
    const maxFin = Math.max(...dia.ventanas.map((v) => parseHora(v.horaFin)));

    const slots: SlotDia[] = [];
    for (let m = minInicio; m < maxFin; m += PASO_MINUTOS) {
      const dentroDeVentana = dia.ventanas.some(
        (v) => m >= parseHora(v.horaInicio) && m < parseHora(v.horaFin)
      );
      if (!dentroDeVentana) continue;

      const bloqueado = dia.bloqueos.some(
        (b) => m >= parseHora(b.horaInicio) && m < parseHora(b.horaFin)
      );
      const turno = dia.turnos.find(
        (t) => m >= parseHora(t.horaInicio) && m < parseHora(t.horaFin)
      );

      slots.push({
        hora: formatHora(m),
        estado: turno ? 'reservado' : bloqueado ? 'bloqueado' : 'libre',
        turno
      });
    }
    return slots;
  });

  constructor(
    public contexto: OdontologoContextoService,
    private turnoService: TurnoService,
    private pacienteService: PacienteService,
    private tipoTratamientoService: TipoTratamientoService,
    private disponibilidadService: DisponibilidadService
  ) {
    // Cambió el odontólogo elegido en el navbar: recargamos todo.
    effect(() => {
      this.contexto.seleccionadoId();
      this.cargarMes();
      this.diaDetalle.set(null);
      this.diaSeleccionado.set(null);
    });
  }

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.pacienteService.getAll().then((v) => this.pacientes.set(v)),
      this.tipoTratamientoService.getAll().then((v) => this.tiposTratamiento.set(v))
    ]);
  }

  async cargarMes(): Promise<void> {
    const odontologoId = this.contexto.seleccionadoId();
    if (!odontologoId) return;

    this.cargandoMes.set(true);
    this.error.set(null);
    try {
      this.turnosDelMes.set(
        await this.turnoService.getAll(inicioDeMes(this.mesActual()), finDeMes(this.mesActual()), odontologoId)
      );
    } catch {
      this.error.set('No se pudieron cargar los turnos del mes.');
    } finally {
      this.cargandoMes.set(false);
    }
  }

  cambiarMes(delta: number): void {
    const actual = this.mesActual();
    this.mesActual.set(new Date(actual.getFullYear(), actual.getMonth() + delta, 1));
    this.diaSeleccionado.set(null);
    this.diaDetalle.set(null);
    this.cargarMes();
  }

  cantidadEnDia(d: Date): number {
    return this.cantidadPorDia().get(claveFecha(d)) ?? 0;
  }

  esHoy(d: Date): boolean {
    const hoy = new Date();
    return d.toDateString() === hoy.toDateString();
  }

  esDiaSeleccionado(d: Date): boolean {
    const sel = this.diaSeleccionado();
    return !!sel && sel.toDateString() === d.toDateString();
  }

  async seleccionarDia(d: Date): Promise<void> {
    this.diaSeleccionado.set(d);
    this.slotParaReservar.set(null);
    this.turnoSeleccionado.set(null);
    this.errorTurno.set(null);
    await this.cargarDia();
  }

  async cargarDia(): Promise<void> {
    const odontologoId = this.contexto.seleccionadoId();
    const dia = this.diaSeleccionado();
    if (!odontologoId || !dia) return;

    this.cargandoDia.set(true);
    try {
      this.diaDetalle.set(await this.turnoService.getDia(odontologoId, dia));
    } catch {
      this.error.set('No se pudo cargar la disponibilidad de ese día.');
    } finally {
      this.cargandoDia.set(false);
    }
  }

  async clicSlot(slot: SlotDia): Promise<void> {
    this.errorTurno.set(null);

    if (slot.estado === 'reservado') {
      this.turnoSeleccionado.set(slot.turno ?? null);
      this.slotParaReservar.set(null);
      return;
    }

    if (this.modoBloqueo()) {
      await this.alternarBloqueoSlot(slot);
      return;
    }

    if (slot.estado === 'libre') {
      this.turnoSeleccionado.set(null);
      this.slotParaReservar.set(slot.hora);
      this.nuevoPacienteId = '';
      this.nuevoTipoTratamientoId = '';
      this.horaHasta = formatHora(parseHora(slot.hora) + PASO_MINUTOS);
    }
  }

  // Solo como comodidad: al elegir un tipo de tratamiento, proponemos el
  // "hasta" según su duración habitual, pero se puede seguir editando a mano.
  onCambioTratamiento(): void {
    const inicio = this.slotParaReservar();
    if (!inicio) return;

    const tratamiento = this.tiposTratamiento().find((t) => t.id === this.nuevoTipoTratamientoId);
    const duracion = tratamiento?.duracionMinutos ?? PASO_MINUTOS;
    this.horaHasta = formatHora(parseHora(inicio) + duracion);
  }

  async alternarBloqueoSlot(slot: SlotDia): Promise<void> {
    const odontologoId = this.contexto.seleccionadoId();
    const dia = this.diaSeleccionado();
    if (!odontologoId || !dia) return;

    this.procesandoBloqueo.set(true);
    try {
      if (slot.estado === 'bloqueado') {
        const bloqueo = this.diaDetalle()?.bloqueos.find(
          (b) => parseHora(slot.hora) >= parseHora(b.horaInicio) && parseHora(slot.hora) < parseHora(b.horaFin)
        );
        if (bloqueo) await this.disponibilidadService.eliminar(bloqueo.id);
      } else {
        const fin = formatHora(parseHora(slot.hora) + PASO_MINUTOS);
        await this.disponibilidadService.crear({
          odontologoId,
          tipo: 'Excepcion',
          fecha: claveFecha(dia),
          todoElDia: false,
          horaInicio: `${slot.hora}:00`,
          horaFin: `${fin}:00`,
          bloqueado: true
        });
      }
      await this.cargarDia();
    } catch {
      this.error.set('No se pudo actualizar el bloqueo.');
    } finally {
      this.procesandoBloqueo.set(false);
    }
  }

  async alternarBloqueoDiaCompleto(): Promise<void> {
    const odontologoId = this.contexto.seleccionadoId();
    const dia = this.diaSeleccionado();
    const detalle = this.diaDetalle();
    if (!odontologoId || !dia || !detalle) return;

    this.procesandoBloqueo.set(true);
    try {
      if (detalle.todoElDiaBloqueado && detalle.todoElDiaBloqueadoId) {
        await this.disponibilidadService.eliminar(detalle.todoElDiaBloqueadoId);
      } else {
        await this.disponibilidadService.crear({
          odontologoId,
          tipo: 'Excepcion',
          fecha: claveFecha(dia),
          todoElDia: true,
          bloqueado: true
        });
      }
      await this.cargarDia();
    } catch {
      this.error.set('No se pudo bloquear/desbloquear el día completo.');
    } finally {
      this.procesandoBloqueo.set(false);
    }
  }

  cancelarReserva(): void {
    this.slotParaReservar.set(null);
    this.errorTurno.set(null);
  }

  async confirmarReserva(): Promise<void> {
    const odontologoId = this.contexto.seleccionadoId();
    const dia = this.diaSeleccionado();
    const hora = this.slotParaReservar();
    if (!odontologoId || !dia || !hora || !this.nuevoPacienteId) return;

    const duracionMinutos = parseHora(this.horaHasta) - parseHora(hora);
    if (!this.horaHasta || duracionMinutos <= 0) {
      this.errorTurno.set('La hora de fin tiene que ser posterior a la de inicio.');
      return;
    }

    this.errorTurno.set(null);
    this.guardandoTurno.set(true);
    try {
      await this.turnoService.crear({
        pacienteId: this.nuevoPacienteId,
        odontologoId,
        tipoTratamientoId: this.nuevoTipoTratamientoId || undefined,
        fechaHora: new Date(`${claveFecha(dia)}T${hora}:00`).toISOString(),
        duracionMinutos
      });
      this.slotParaReservar.set(null);
      await Promise.all([this.cargarDia(), this.cargarMes()]);
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorTurno.set(httpError?.error?.message ?? 'No se pudo reservar el turno.');
    } finally {
      this.guardandoTurno.set(false);
    }
  }

  cerrarDetalleTurno(): void {
    this.turnoSeleccionado.set(null);
  }

  async cambiarEstadoSeleccionado(estado: TurnoEstado): Promise<void> {
    const turno = this.turnoSeleccionado();
    if (!turno) return;

    this.cambiandoEstado.set(true);
    try {
      await this.turnoService.cambiarEstado(turno.id, estado);
      this.turnoSeleccionado.set({ ...turno, estado });
      await Promise.all([this.cargarDia(), this.cargarMes()]);
    } catch {
      this.error.set('No se pudo cambiar el estado del turno.');
    } finally {
      this.cambiandoEstado.set(false);
    }
  }

  nombreTratamiento(id: string | null): string {
    if (!id) return '-';
    return this.tiposTratamiento().find((t) => t.id === id)?.nombre ?? '(desconocido)';
  }
}
