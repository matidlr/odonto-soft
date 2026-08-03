import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Disponibilidad,
  DisponibilidadService,
  DiaSemana,
  TipoDisponibilidad
} from '../../core/disponibilidad.service';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Sede, SedeService } from '../../core/sede.service';

const DIAS: DiaSemana[] = ['Lunes', 'Martes', 'Miercoles', 'Jueves', 'Viernes', 'Sabado', 'Domingo'];

@Component({
  selector: 'app-disponibilidad',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './disponibilidad.component.html',
  styleUrl: './disponibilidad.component.scss'
})
export class DisponibilidadComponent implements OnInit {
  dias = DIAS;

  odontologos = signal<Odontologo[]>([]);
  sedes = signal<Sede[]>([]);
  reglas = signal<Disponibilidad[]>([]);
  cargando = signal(true);
  mostrarForm = signal(false);
  guardando = signal(false);
  error = signal<string | null>(null);

  odontologoSeleccionado = '';
  sedeSeleccionada = '';

  // Formulario de alta
  tipo: TipoDisponibilidad = 'Recurrente';
  diaSemana: DiaSemana = 'Lunes';
  fecha = '';
  todoElDia = false;
  horaInicio = '09:00';
  horaFin = '13:00';
  bloqueado = false;

  constructor(
    private disponibilidadService: DisponibilidadService,
    private odontologoService: OdontologoService,
    private sedeService: SedeService,
    public contexto: OdontologoContextoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.odontologos.set(await this.odontologoService.getAll());
    if (this.odontologos().length > 0) {
      // Arrancamos con el odontólogo elegido en el navbar, si hay uno.
      const seleccionadoEnNavbar = this.contexto.seleccionadoId();
      this.odontologoSeleccionado =
        (seleccionadoEnNavbar && this.odontologos().some((o) => o.id === seleccionadoEnNavbar)
          ? seleccionadoEnNavbar
          : this.odontologos()[0].id);
      await this.cambiarOdontologo();
    } else {
      this.cargando.set(false);
    }
  }

  async cambiarOdontologo(): Promise<void> {
    this.sedes.set(await this.sedeService.getAll(this.odontologoSeleccionado));
    const principal = this.sedes().find((s) => s.esPrincipal) ?? this.sedes()[0];
    this.sedeSeleccionada = principal?.id ?? '';
    await this.cargarReglas();
  }

  async cargarReglas(): Promise<void> {
    if (!this.odontologoSeleccionado) return;
    this.cargando.set(true);
    try {
      this.reglas.set(await this.disponibilidadService.getAll(this.odontologoSeleccionado, this.sedeSeleccionada || undefined));
    } finally {
      this.cargando.set(false);
    }
  }

  async crear(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.disponibilidadService.crear({
        odontologoId: this.odontologoSeleccionado,
        sedeId: this.sedeSeleccionada || undefined,
        tipo: this.tipo,
        diaSemana: this.tipo === 'Recurrente' ? this.diaSemana : undefined,
        fecha: this.tipo === 'Excepcion' ? this.fecha : undefined,
        todoElDia: this.todoElDia,
        horaInicio: this.todoElDia ? undefined : `${this.horaInicio}:00`,
        horaFin: this.todoElDia ? undefined : `${this.horaFin}:00`,
        bloqueado: this.bloqueado
      });
      this.mostrarForm.set(false);
      await this.cargarReglas();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar la regla.');
    } finally {
      this.guardando.set(false);
    }
  }

  async eliminar(id: string): Promise<void> {
    try {
      await this.disponibilidadService.eliminar(id);
      await this.cargarReglas();
    } catch {
      this.error.set('No se pudo eliminar la regla.');
    }
  }
}
