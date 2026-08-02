import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Cobro, CobroService, MedioPago, SaldoPaciente } from '../../core/cobro.service';
import { Odontologo, OdontologoService } from '../../core/odontologo.service';
import { Paciente, PacienteService } from '../../core/paciente.service';
import { Presupuesto, PresupuestoService } from '../../core/presupuesto.service';

const MEDIOS_PAGO: MedioPago[] = ['Efectivo', 'Transferencia', 'Tarjeta', 'Qr'];

const ETIQUETA_MEDIO: Record<MedioPago, string> = {
  Efectivo: 'Efectivo',
  Transferencia: 'Transferencia',
  Tarjeta: 'Tarjeta',
  Qr: 'QR'
};

@Component({
  selector: 'app-cobros-paciente',
  standalone: true,
  imports: [FormsModule, DatePipe, CurrencyPipe, RouterLink],
  templateUrl: './cobros-paciente.component.html',
  styleUrl: './cobros-paciente.component.scss'
})
export class CobrosPacienteComponent implements OnInit {
  mediosPago = MEDIOS_PAGO;
  etiquetaMedio = ETIQUETA_MEDIO;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  cobros = signal<Cobro[]>([]);
  saldo = signal<SaldoPaciente | null>(null);
  presupuestosAprobados = signal<Presupuesto[]>([]);
  odontologos = signal<Odontologo[]>([]);
  cargando = signal(true);

  mostrarForm = signal(false);
  monto = 0;
  medioPago: MedioPago = 'Efectivo';
  concepto = '';
  presupuestoId = '';
  odontologoId = '';
  guardando = signal(false);
  error = signal<string | null>(null);

  borrandoId = signal<string | null>(null);

  saldoColor = computed(() => {
    const s = this.saldo();
    if (!s) return '';
    return s.saldo > 0 ? 'positivo' : 'saldado';
  });

  constructor(
    private route: ActivatedRoute,
    private cobroService: CobroService,
    private pacienteService: PacienteService,
    private presupuestoService: PresupuestoService,
    private odontologoService: OdontologoService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    await this.cargarTodo();
  }

  async cargarTodo(): Promise<void> {
    this.cargando.set(true);
    try {
      const [cobros, saldo, pacientes, presupuestos, odontologos] = await Promise.all([
        this.cobroService.getPorPaciente(this.pacienteId),
        this.cobroService.getSaldo(this.pacienteId),
        this.pacienteService.getAll(),
        this.presupuestoService.getPorPaciente(this.pacienteId),
        this.odontologoService.getAll()
      ]);
      this.cobros.set(cobros);
      this.saldo.set(saldo);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
      this.presupuestosAprobados.set(presupuestos.filter((p) => p.estado === 'Aprobado'));
      this.odontologos.set(odontologos);
    } finally {
      this.cargando.set(false);
    }
  }

  abrirNuevo(): void {
    this.monto = 0;
    this.medioPago = 'Efectivo';
    this.concepto = '';
    this.presupuestoId = '';
    this.odontologoId = '';
    this.error.set(null);
    this.mostrarForm.set(true);
  }

  cancelar(): void {
    this.mostrarForm.set(false);
  }

  async guardar(): Promise<void> {
    this.error.set(null);
    if (this.monto <= 0) {
      this.error.set('El monto tiene que ser mayor a 0.');
      return;
    }
    this.guardando.set(true);
    try {
      await this.cobroService.crear(this.pacienteId, {
        monto: this.monto,
        medioPago: this.medioPago,
        concepto: this.concepto || undefined,
        presupuestoId: this.presupuestoId || undefined,
        odontologoId: this.odontologoId || undefined
      });
      this.mostrarForm.set(false);
      await this.cargarTodo();
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo registrar el cobro.');
    } finally {
      this.guardando.set(false);
    }
  }

  async borrar(c: Cobro): Promise<void> {
    if (!confirm('¿Borrar este cobro?')) return;
    this.borrandoId.set(c.id);
    try {
      await this.cobroService.borrar(c.id);
      await this.cargarTodo();
    } finally {
      this.borrandoId.set(null);
    }
  }

  nombreOdontologo(id: string | null): string {
    if (!id) return '';
    return this.odontologos().find((o) => o.id === id)?.nombre ?? '';
  }
}
