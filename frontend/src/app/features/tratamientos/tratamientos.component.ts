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

  nombre = '';
  duracionMinutos = 30;
  precioBase = 0;

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

  async crear(): Promise<void> {
    this.error.set(null);
    this.guardando.set(true);
    try {
      await this.tipoTratamientoService.crear({
        nombre: this.nombre,
        duracionMinutos: this.duracionMinutos,
        precioBase: this.precioBase
      });
      this.nombre = '';
      this.duracionMinutos = 30;
      this.precioBase = 0;
      this.mostrarForm.set(false);
      await this.cargar();
    } catch {
      this.error.set('No se pudo crear el tipo de tratamiento.');
    } finally {
      this.guardando.set(false);
    }
  }
}
