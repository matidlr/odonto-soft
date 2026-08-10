import { Component, OnInit, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

export interface GrupoPacientes {
  letra: string;
  pacientes: Paciente[];
}

@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './pacientes.component.html',
  styleUrl: './pacientes.component.scss'
})
export class PacientesComponent implements OnInit {
  pacientes = signal<Paciente[]>([]);
  cargando = signal(true);
  mostrarForm = signal(false);
  guardando = signal(false);
  error = signal<string | null>(null);
  busqueda = signal('');
  mostrarInactivos = signal(false);

  nombre = '';
  apellido = '';
  dni = '';
  telefono = '';
  email = '';
  fechaNacimiento = '';
  odontologoPrincipalId = '';

  pacientesFiltrados = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    if (!texto) return this.pacientes();

    return this.pacientes().filter(
      (p) =>
        p.nombre.toLowerCase().includes(texto) ||
        (p.apellido ?? '').toLowerCase().includes(texto) ||
        (p.dni ?? '').toLowerCase().includes(texto) ||
        (p.telefono ?? '').toLowerCase().includes(texto)
    );
  });

  // Agrupa por la inicial del apellido (o del nombre si no tiene apellido
  // cargado, para no perder pacientes viejos) — la API ya entrega la lista
  // en ese mismo orden, así que acá solo se arman los grupos, no se
  // reordena nada.
  grupos = computed<GrupoPacientes[]>(() => {
    const mapa = new Map<string, Paciente[]>();
    for (const p of this.pacientesFiltrados()) {
      const letra = this.letraDe(p);
      if (!mapa.has(letra)) mapa.set(letra, []);
      mapa.get(letra)!.push(p);
    }
    return Array.from(mapa.entries())
      .sort(([a], [b]) => a.localeCompare(b, 'es'))
      .map(([letra, pacientes]) => ({ letra, pacientes }));
  });

  letraDe(p: Paciente): string {
    const base = (p.apellido?.trim() || p.nombre).trim();
    return base ? base[0].toUpperCase() : '#';
  }

  nombreCompleto(p: Paciente): string {
    return p.apellido?.trim() ? `${p.apellido}, ${p.nombre}` : p.nombre;
  }

  constructor(
    private pacienteService: PacienteService,
    public contexto: OdontologoContextoService
  ) {
    // Solo filtramos por odontólogo cuando hay más de uno: si es una
    // clínica de un solo profesional, no tiene sentido esconder pacientes
    // que todavía no tienen "odontólogo principal" asignado.
    effect(() => {
      this.contexto.seleccionadoId();
      this.cargar();
    });
  }

  async ngOnInit(): Promise<void> {
    // El primer cargar() ya lo dispara el effect del constructor.
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      const odontologoId = this.contexto.hayMasDeUno()
        ? (this.contexto.seleccionadoId() ?? undefined)
        : undefined;
      this.pacientes.set(await this.pacienteService.getAll(odontologoId, this.mostrarInactivos()));
    } finally {
      this.cargando.set(false);
    }
  }

  async alternarMostrarInactivos(): Promise<void> {
    this.mostrarInactivos.set(!this.mostrarInactivos());
    await this.cargar();
  }

  abrirNuevo(): void {
    this.limpiarForm();
    this.odontologoPrincipalId = this.contexto.seleccionadoId() ?? '';
    this.mostrarForm.set(true);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
  }

  private limpiarForm(): void {
    this.nombre = this.apellido = this.dni = this.telefono = this.email = this.fechaNacimiento = '';
    this.odontologoPrincipalId = '';
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.pacienteService.crear({
        nombre: this.nombre,
        apellido: this.apellido || undefined,
        dni: this.dni || undefined,
        telefono: this.telefono || undefined,
        email: this.email || undefined,
        fechaNacimiento: this.fechaNacimiento || undefined,
        odontologoPrincipalId: this.odontologoPrincipalId || undefined
      });

      this.limpiarForm();
      this.mostrarForm.set(false);
      await this.cargar();
    } catch (err: unknown) {
      const httpError = err as { status?: number; error?: { message?: string }; message?: string };
      this.error.set(
        `No se pudo guardar el paciente. (status ${httpError?.status ?? '?'}: ${httpError?.error?.message ?? httpError?.message ?? 'sin detalle'})`
      );
    } finally {
      this.guardando.set(false);
    }
  }
}
