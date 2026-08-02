import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CategoriaInsumo,
  Insumo,
  InsumoService,
  MovimientoStock
} from '../../core/insumo.service';

const CATEGORIAS: CategoriaInsumo[] = ['Anestesia', 'Guantes', 'Resinas', 'Implantes', 'Materiales', 'Otro'];

function insumoVacio() {
  return { nombre: '', categoria: 'Materiales' as CategoriaInsumo, unidad: 'unidades', stockMinimo: 0, stockInicial: 0, activo: true };
}

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [FormsModule, DecimalPipe, DatePipe],
  templateUrl: './inventario.component.html',
  styleUrl: './inventario.component.scss'
})
export class InventarioComponent implements OnInit {
  categorias = CATEGORIAS;

  insumos = signal<Insumo[]>([]);
  cargando = signal(true);

  filtroCategoria = signal<CategoriaInsumo | 'Todas'>('Todas');
  soloStockBajo = signal(false);

  insumosFiltrados = computed(() => {
    let lista = this.insumos();
    const cat = this.filtroCategoria();
    if (cat !== 'Todas') lista = lista.filter((i) => i.categoria === cat);
    if (this.soloStockBajo()) lista = lista.filter((i) => i.stockBajo);
    return lista;
  });

  cantidadAlertas = computed(() => this.insumos().filter((i) => i.stockBajo).length);

  mostrarForm = signal(false);
  editandoId = signal<string | null>(null);
  form = insumoVacio();
  guardando = signal(false);
  error = signal<string | null>(null);

  movimientosDe = signal<Insumo | null>(null);
  movimientos = signal<MovimientoStock[]>([]);
  cantidadMovimiento = 0;
  motivoMovimiento = '';
  guardandoMovimiento = signal(false);
  errorMovimiento = signal<string | null>(null);

  constructor(private insumoService: InsumoService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      this.insumos.set(await this.insumoService.getAll(true));
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.editandoId.set(null);
    this.form = insumoVacio();
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  editar(i: Insumo): void {
    this.editandoId.set(i.id);
    this.form = { nombre: i.nombre, categoria: i.categoria, unidad: i.unidad, stockMinimo: i.stockMinimo, stockInicial: 0, activo: i.activo };
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
        await this.insumoService.editar(id, {
          nombre: this.form.nombre,
          categoria: this.form.categoria,
          unidad: this.form.unidad,
          stockMinimo: this.form.stockMinimo,
          activo: this.form.activo
        });
      } else {
        await this.insumoService.crear({
          nombre: this.form.nombre,
          categoria: this.form.categoria,
          unidad: this.form.unidad,
          stockMinimo: this.form.stockMinimo,
          stockInicial: this.form.stockInicial
        });
      }
      this.mostrarForm.set(false);
      await this.cargar();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo guardar el insumo.');
    } finally {
      this.guardando.set(false);
    }
  }

  async abrirMovimientos(i: Insumo): Promise<void> {
    this.movimientosDe.set(i);
    this.cantidadMovimiento = 0;
    this.motivoMovimiento = '';
    this.errorMovimiento.set(null);
    this.movimientos.set(await this.insumoService.getMovimientos(i.id));
  }

  cerrarMovimientos(): void {
    this.movimientosDe.set(null);
  }

  async registrarMovimiento(signo: 1 | -1): Promise<void> {
    const insumo = this.movimientosDe();
    if (!insumo || this.cantidadMovimiento <= 0) return;

    this.errorMovimiento.set(null);
    this.guardandoMovimiento.set(true);
    try {
      const resultado = await this.insumoService.crearMovimiento(insumo.id, {
        cantidad: this.cantidadMovimiento * signo,
        motivo: this.motivoMovimiento || undefined
      });
      this.movimientosDe.set(resultado.insumo);
      this.movimientos.set(await this.insumoService.getMovimientos(insumo.id));
      this.cantidadMovimiento = 0;
      this.motivoMovimiento = '';
      await this.cargar();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.errorMovimiento.set(httpError?.error?.message ?? 'No se pudo registrar el movimiento.');
    } finally {
      this.guardandoMovimiento.set(false);
    }
  }
}
