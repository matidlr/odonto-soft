import { DecimalPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TipoTratamiento, TipoTratamientoService } from '../../core/tipo-tratamiento.service';

@Component({
  selector: 'app-tratamientos',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './tratamientos.component.html',
  styleUrl: './tratamientos.component.scss'
})
export class TratamientosComponent implements OnInit {
  tratamientos = signal<TipoTratamiento[]>([]);
  cargando = signal(true);
  mostrarForm = signal(false);
  guardando = signal(false);
  error = signal<string | null>(null);

  editandoId = signal<string | null>(null);

  nombre = '';
  duracionMinutos = 30;
  precioBase = 0;
  observaciones = '';

  constructor(private tipoTratamientoService: TipoTratamientoService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      this.tratamientos.set(await this.tipoTratamientoService.getAll());
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.editandoId.set(null);
    this.nombre = '';
    this.duracionMinutos = 30;
    this.precioBase = 0;
    this.observaciones = '';
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  editar(t: TipoTratamiento): void {
    this.editandoId.set(t.id);
    this.nombre = t.nombre;
    this.duracionMinutos = t.duracionMinutos;
    this.precioBase = t.precioBase;
    this.observaciones = t.observaciones ?? '';
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
        duracionMinutos: this.duracionMinutos,
        precioBase: this.precioBase,
        observaciones: this.observaciones || undefined
      };

      const id = this.editandoId();
      if (id) {
        await this.tipoTratamientoService.editar(id, datos);
      } else {
        await this.tipoTratamientoService.crear(datos);
      }

      this.mostrarForm.set(false);
      await this.cargar();
    } catch {
      this.error.set('No se pudo guardar el tipo de tratamiento.');
    } finally {
      this.guardando.set(false);
    }
  }
}
