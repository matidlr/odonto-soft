import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Estadisticas, EstadisticaService } from '../../core/estadistica.service';

const NOMBRES_MES = [
  'Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'
];

function primerDiaDelMes(): string {
  const hoy = new Date();
  return new Date(hoy.getFullYear(), hoy.getMonth(), 1).toISOString().slice(0, 10);
}

function hoyIso(): string {
  return new Date().toISOString().slice(0, 10);
}

@Component({
  selector: 'app-estadisticas',
  standalone: true,
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './estadisticas.component.html',
  styleUrl: './estadisticas.component.scss'
})
export class EstadisticasComponent implements OnInit {
  desde = primerDiaDelMes();
  hasta = hoyIso();

  datos = signal<Estadisticas | null>(null);
  cargando = signal(true);

  maxFacturacionMes = computed(() => {
    const meses = this.datos()?.facturacionPorMes ?? [];
    return Math.max(1, ...meses.map((m) => m.total));
  });

  maxRankingTratamiento = computed(() => {
    const items = this.datos()?.tratamientosMasRealizados ?? [];
    return Math.max(1, ...items.map((i) => i.cantidad));
  });

  maxRankingOdontologo = computed(() => {
    const items = this.datos()?.odontologosConMasConsultas ?? [];
    return Math.max(1, ...items.map((i) => i.cantidad));
  });

  constructor(private estadisticaService: EstadisticaService) {}

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      this.datos.set(await this.estadisticaService.get(this.desde, this.hasta));
    } finally {
      this.cargando.set(false);
    }
  }

  nombreMes(anio: number, mes: number): string {
    return `${NOMBRES_MES[mes - 1]} ${anio}`;
  }
}
