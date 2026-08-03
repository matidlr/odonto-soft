import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { OdontologoContextoService } from '../../core/odontologo-contexto.service';
import { EditarSedeRequest, Sede, SedeService } from '../../core/sede.service';

@Component({
  selector: 'app-sedes',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './sedes.component.html',
  styleUrl: './sedes.component.scss'
})
export class SedesComponent implements OnInit {
  odontologos = signal<Odontologo[]>([]);
  sedes = signal<Sede[]>([]);
  cargando = signal(true);

  odontologoSeleccionado = '';

  mostrarForm = signal(false);
  editandoId = signal<string | null>(null);
  nombre = '';
  direccion = '';
  activa = true;
  guardando = signal(false);
  error = signal<string | null>(null);

  constructor(
    private odontologoService: OdontologoService,
    private sedeService: SedeService,
    public contexto: OdontologoContextoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.odontologos.set(await this.odontologoService.getAll());
    if (this.odontologos().length > 0) {
      const seleccionadoEnNavbar = this.contexto.seleccionadoId();
      this.odontologoSeleccionado =
        seleccionadoEnNavbar && this.odontologos().some((o) => o.id === seleccionadoEnNavbar)
          ? seleccionadoEnNavbar
          : this.odontologos()[0].id;
      await this.cargarSedes();
    } else {
      this.cargando.set(false);
    }
  }

  async cargarSedes(): Promise<void> {
    if (!this.odontologoSeleccionado) return;
    this.cargando.set(true);
    try {
      this.sedes.set(await this.sedeService.getAll(this.odontologoSeleccionado, true));
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.editandoId.set(null);
    this.nombre = '';
    this.direccion = '';
    this.activa = true;
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  editar(s: Sede): void {
    this.editandoId.set(s.id);
    this.nombre = s.nombre;
    this.direccion = s.direccion ?? '';
    this.activa = s.activa;
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
      const id = this.editandoId();
      if (id) {
        const datos: EditarSedeRequest = { nombre: this.nombre, direccion: this.direccion || undefined, activa: this.activa };
        await this.sedeService.editar(id, datos);
      } else {
        await this.sedeService.crear({
          odontologoId: this.odontologoSeleccionado,
          nombre: this.nombre,
          direccion: this.direccion || undefined
        });
      }
      this.mostrarForm.set(false);
      await this.cargarSedes();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar la sede.');
    } finally {
      this.guardando.set(false);
    }
  }
}
