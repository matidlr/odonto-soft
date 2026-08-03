import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';

@Component({
  selector: 'app-odontologos',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './odontologos.component.html',
  styleUrl: './odontologos.component.scss'
})
export class OdontologosComponent implements OnInit {
  mostrarForm = signal(false);
  editandoId = signal<string | null>(null);
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

  abrirNuevo(): void {
    this.editandoId.set(null);
    this.nombre = '';
    this.matricula = '';
    this.especialidad = '';
    this.colorAgenda = '#2563eb';
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  editar(o: Odontologo): void {
    this.editandoId.set(o.id);
    this.nombre = o.nombre;
    this.matricula = o.matricula;
    this.especialidad = o.especialidad ?? '';
    this.colorAgenda = o.colorAgenda;
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      const datos = {
        nombre: this.nombre,
        matricula: this.matricula,
        especialidad: this.especialidad || undefined,
        colorAgenda: this.colorAgenda || undefined
      };

      const id = this.editandoId();
      if (id) {
        await this.odontologoService.editar(id, datos);
      } else {
        await this.odontologoService.crear(datos);
      }

      this.mostrarForm.set(false);
      // Recargamos el contexto compartido para que el selector del navbar
      // vea los cambios de inmediato.
      await this.contexto.cargar();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar el odontólogo.');
    } finally {
      this.guardando.set(false);
    }
  }
}
