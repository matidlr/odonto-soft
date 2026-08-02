import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { OdontologoService } from '../../core/odontologo.service';

@Component({
  selector: 'app-odontologos',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './odontologos.component.html',
  styleUrl: './odontologos.component.scss'
})
export class OdontologosComponent implements OnInit {
  mostrarForm = signal(false);
  guardando = signal(false);
  error = signal<string | null>(null);

  nombre = '';
  matricula = '';
  especialidad = '';
  colorAgenda = '#2563eb';

  constructor(
    public contexto: OdontologoContextoService,
    private odontologoService: OdontologoService
  ) {}

  async ngOnInit(): Promise<void> {
    if (!this.contexto.cargado()) {
      await this.contexto.cargar();
    }
  }

  async crear(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.odontologoService.crear({
        nombre: this.nombre,
        matricula: this.matricula,
        especialidad: this.especialidad || undefined,
        colorAgenda: this.colorAgenda || undefined
      });
      this.nombre = '';
      this.matricula = '';
      this.especialidad = '';
      this.colorAgenda = '#2563eb';
      this.mostrarForm.set(false);
      // Recargamos el contexto compartido para que el selector del navbar
      // vea al odontólogo nuevo de inmediato.
      await this.contexto.cargar();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo agregar el odontólogo.');
    } finally {
      this.guardando.set(false);
    }
  }
}
