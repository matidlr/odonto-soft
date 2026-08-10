import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

@Component({
  selector: 'app-ficha-paciente',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './ficha-paciente.component.html',
  styleUrl: './ficha-paciente.component.scss'
})
export class FichaPacienteComponent implements OnInit {
  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  cargando = signal(true);
  error = signal<string | null>(null);

  editando = signal(false);
  guardando = signal(false);
  accionando = signal(false);

  nombre = '';
  dni = '';
  telefono = '';
  email = '';
  fechaNacimiento = '';
  odontologoPrincipalId = '';

  odontologoPrincipalNombre = computed(() => {
    const id = this.paciente()?.odontologoPrincipalId;
    if (!id) return '(sin asignar)';
    return this.contexto.odontologos().find((o) => o.id === id)?.nombre ?? '(sin asignar)';
  });

  secciones = [
    { path: 'odontograma', etiqueta: 'Odontograma', icono: '🦷' },
    { path: 'historial-clinico', etiqueta: 'Historia clínica', icono: '📋' },
    { path: 'archivos', etiqueta: 'Archivos', icono: '📎' },
    { path: 'presupuestos', etiqueta: 'Presupuestos', icono: '💰' },
    { path: 'cobros', etiqueta: 'Cobros', icono: '💳' },
    { path: 'consentimientos', etiqueta: 'Consentimientos', icono: '✍️' },
    { path: 'auditoria', etiqueta: 'Auditoría', icono: '🕒' }
  ];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private pacienteService: PacienteService,
    public contexto: OdontologoContextoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId') ?? '';
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    this.error.set(null);
    try {
      const paciente = await this.pacienteService.getById(this.pacienteId);
      this.paciente.set(paciente);
    } catch {
      this.error.set('No se pudo cargar el paciente.');
    } finally {
      this.cargando.set(false);
    }
  }

  abrirEdicion(): void {
    const p = this.paciente();
    if (!p) return;
    this.nombre = p.nombre;
    this.dni = p.dni ?? '';
    this.telefono = p.telefono ?? '';
    this.email = p.email ?? '';
    this.fechaNacimiento = p.fechaNacimiento ?? '';
    this.odontologoPrincipalId = p.odontologoPrincipalId ?? '';
    this.error.set(null);
    this.editando.set(true);
  }

  cancelarEdicion(): void {
    this.editando.set(false);
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.pacienteService.editar(this.pacienteId, {
        nombre: this.nombre,
        dni: this.dni || undefined,
        telefono: this.telefono || undefined,
        email: this.email || undefined,
        fechaNacimiento: this.fechaNacimiento || undefined,
        odontologoPrincipalId: this.odontologoPrincipalId || undefined
      });
      this.editando.set(false);
      await this.cargar();
    } catch (err: unknown) {
      const httpError = err as { status?: number; error?: { message?: string }; message?: string };
      this.error.set(
        `No se pudo guardar. (status ${httpError?.status ?? '?'}: ${httpError?.error?.message ?? httpError?.message ?? 'sin detalle'})`
      );
    } finally {
      this.guardando.set(false);
    }
  }

  async darDeBaja(): Promise<void> {
    if (!confirm('¿Dar de baja a este paciente?')) return;
    this.accionando.set(true);
    try {
      await this.pacienteService.eliminar(this.pacienteId);
      await this.cargar();
    } finally {
      this.accionando.set(false);
    }
  }

  async reactivar(): Promise<void> {
    this.accionando.set(true);
    try {
      await this.pacienteService.reactivar(this.pacienteId);
      await this.cargar();
    } finally {
      this.accionando.set(false);
    }
  }

  volver(): void {
    this.router.navigate(['/pacientes']);
  }
}
