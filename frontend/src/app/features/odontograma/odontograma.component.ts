import { Component, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EstadoDiente, EstadoPieza, EstadoTratamiento, OdontogramaService } from '../../core/odontograma.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

// Numeración FDI en el orden habitual del odontograma visual (mirando al
// paciente de frente): arriba de derecha del paciente a izquierda, abajo
// de derecha a izquierda también.
const FILA_SUPERIOR = [18, 17, 16, 15, 14, 13, 12, 11, 21, 22, 23, 24, 25, 26, 27, 28];
const FILA_INFERIOR = [48, 47, 46, 45, 44, 43, 42, 41, 31, 32, 33, 34, 35, 36, 37, 38];

// Piezas temporales/de leche (pacientes niños).
const FILA_SUPERIOR_TEMPORAL = [55, 54, 53, 52, 51, 61, 62, 63, 64, 65];
const FILA_INFERIOR_TEMPORAL = [85, 84, 83, 82, 81, 71, 72, 73, 74, 75];

const ESTADOS: EstadoDiente[] = [
  'Sano',
  'Cariado',
  'Obturado',
  'Corona',
  'Endodoncia',
  'Ausente',
  'Implante',
  'Fracturado',
  'Sellador',
  'Ortodoncia'
];

const COLOR_POR_ESTADO: Record<EstadoDiente, string> = {
  Sano: '#e5e7eb',
  Cariado: '#dc2626',
  Obturado: '#2563eb',
  Corona: '#eab308',
  Endodoncia: '#9333ea',
  Ausente: '#9ca3af',
  Implante: '#16a34a',
  Fracturado: '#ea580c',
  Sellador: '#0d9488',
  Ortodoncia: '#db2777'
};

// Rojo = ya realizado, azul = planificado/a realizar (misma convención
// que suelen usar los odontogramas en papel).
const BORDE_POR_ESTADO_TRATAMIENTO: Record<EstadoTratamiento, string> = {
  Realizado: '#dc2626',
  Planificado: '#2563eb'
};

@Component({
  selector: 'app-odontograma',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './odontograma.component.html',
  styleUrl: './odontograma.component.scss'
})
export class OdontogramaComponent implements OnInit {
  filaSuperior = FILA_SUPERIOR;
  filaInferior = FILA_INFERIOR;
  filaSuperiorTemporal = FILA_SUPERIOR_TEMPORAL;
  filaInferiorTemporal = FILA_INFERIOR_TEMPORAL;
  estados = ESTADOS;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  piezas = signal<EstadoPieza[]>([]);
  cargando = signal(true);

  mapaEstados = computed(() => new Map(this.piezas().map((p) => [p.numeroFdi, p])));

  // Mostramos temporales si el paciente es chico (dentición mixta hasta
  // ~12-13 años) o si no cargamos la fecha de nacimiento (por las dudas,
  // mejor mostrar de más que ocultar algo que podría hacer falta).
  mostrarTemporales = computed(() => {
    const fechaNacimiento = this.paciente()?.fechaNacimiento;
    if (!fechaNacimiento) return true;

    const edad = this.calcularEdad(fechaNacimiento);
    return edad < 13;
  });

  constructor(
    private route: ActivatedRoute,
    private odontogramaService: OdontogramaService,
    private pacienteService: PacienteService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    await this.cargarTodo();
  }

  async cargarTodo(): Promise<void> {
    this.cargando.set(true);
    try {
      const [piezas, pacientes] = await Promise.all([
        this.odontogramaService.getEstadoActual(this.pacienteId),
        this.pacienteService.getAll()
      ]);
      this.piezas.set(piezas);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
    } finally {
      this.cargando.set(false);
    }
  }

  colorDe(numero: number): string {
    const pieza = this.mapaEstados().get(numero);
    return COLOR_POR_ESTADO[pieza?.estado ?? 'Sano'];
  }

  bordeDe(numero: number): string {
    const pieza = this.mapaEstados().get(numero);
    if (!pieza?.estadoTratamiento) return 'transparent';
    return BORDE_POR_ESTADO_TRATAMIENTO[pieza.estadoTratamiento];
  }

  colorDeEstado(estado: EstadoDiente): string {
    return COLOR_POR_ESTADO[estado];
  }

  private calcularEdad(fechaNacimientoIso: string): number {
    const nacimiento = new Date(fechaNacimientoIso);
    const hoy = new Date();
    let edad = hoy.getFullYear() - nacimiento.getFullYear();
    const cumplioEsteAnio =
      hoy.getMonth() > nacimiento.getMonth() ||
      (hoy.getMonth() === nacimiento.getMonth() && hoy.getDate() >= nacimiento.getDate());
    if (!cumplioEsteAnio) edad--;
    return edad;
  }
}
