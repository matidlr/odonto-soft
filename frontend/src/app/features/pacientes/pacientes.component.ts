import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Paciente, PacienteService } from '../../core/paciente.service';

@Component({
  selector: 'app-pacientes',
  standalone: true,
  imports: [FormsModule],
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
        email: this.email || undefined
      });
      this.nombre = this.dni = this.telefono = this.email = '';
      this.mostrarForm.set(false);
      await this.cargar();
    } catch {
      this.error.set('No se pudo crear el paciente.');
    } finally {
      this.guardando.set(false);
    }
  }
}
