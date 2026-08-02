import { Component, OnInit, computed, effect, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

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

  // Si tiene valor, el form está editando ese paciente en vez de crear uno nuevo.
  editandoId = signal<string | null>(null);

  nombre = '';
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
        (p.dni ?? '').toLowerCase().includes(texto) ||
        (p.telefono ?? '').toLowerCase().includes(texto)
    );
  });

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
      this.pacientes.set(await this.pacienteService.getAll(odontologoId));
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.editandoId.set(null);
    this.limpiarForm();
    this.odontologoPrincipalId = this.contexto.seleccionadoId() ?? '';
    this.mostrarForm.set(true);
  }

  editar(p: Paciente): void {
    this.editandoId.set(p.id);
    this.nombre = p.nombre;
    this.dni = p.dni ?? '';
    this.telefono = p.telefono ?? '';
    this.email = p.email ?? '';
    this.fechaNacimiento = p.fechaNacimiento ? p.fechaNacimiento.slice(0, 10) : '';
    this.odontologoPrincipalId = p.odontologoPrincipalId ?? '';
    this.mostrarForm.set(true);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
    this.editandoId.set(null);
  }

  private limpiarForm(): void {
    this.nombre = this.dni = this.telefono = this.email = this.fechaNacimiento = '';
    this.odontologoPrincipalId = '';
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      const datos = {
        nombre: this.nombre,
        dni: this.dni || undefined,
        telefono: this.telefono || undefined,
        email: this.email || undefined,
        fechaNacimiento: this.fechaNacimiento || undefined,
        odontologoPrincipalId: this.odontologoPrincipalId || undefined
      };

      const id = this.editandoId();
      if (id) {
        await this.pacienteService.editar(id, datos);
      } else {
        await this.pacienteService.crear(datos);
      }

      this.limpiarForm();
      this.mostrarForm.set(false);
      this.editandoId.set(null);
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
