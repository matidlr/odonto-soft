import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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

  nombre = '';
  dni = '';
  telefono = '';
  email = '';
  fechaNacimiento = '';

  constructor(private pacienteService: PacienteService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      this.pacientes.set(await this.pacienteService.getAll());
    } finally {
      this.cargando.set(false);
    }
  }

  async crear(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.pacienteService.crear({
        nombre: this.nombre,
        dni: this.dni || undefined,
        telefono: this.telefono || undefined,
        email: this.email || undefined,
        fechaNacimiento: this.fechaNacimiento || undefined
      });
      this.nombre = this.dni = this.telefono = this.email = this.fechaNacimiento = '';
      this.mostrarForm.set(false);
      await this.cargar();
    } catch (err: unknown) {
      console.error('Error al crear paciente:', err);
      const httpError = err as { status?: number; error?: { message?: string }; message?: string };
      this.error.set(
        `No se pudo crear el paciente. (status ${httpError?.status ?? '?'}: ${httpError?.error?.message ?? httpError?.message ?? 'sin detalle'})`
      );
    } finally {
      this.guardando.set(false);
    }
  }
}
