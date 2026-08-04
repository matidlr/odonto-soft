import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HistorialClinicoService, RegistroAuditoria } from '../../core/historial-clinico.service';
import { Paciente, PacienteService } from '../../core/paciente.service';

const ETIQUETA_ENTIDAD: Record<string, string> = {
  EventoOdontograma: 'Odontograma',
  FichaMedica: 'Ficha médica',
  NotaEvolucion: 'Nota de evolución',
  Paciente: 'Datos del paciente',
  Turno: 'Turno',
  Cobro: 'Cobro',
  Presupuesto: 'Presupuesto',
  ArchivoPaciente: 'Archivos',
  Consentimiento: 'Consentimiento'
};

@Component({
  selector: 'app-auditoria',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './auditoria.component.html',
  styleUrl: './auditoria.component.scss'
})
export class AuditoriaComponent implements OnInit {
  etiquetaEntidad = ETIQUETA_ENTIDAD;

  pacienteId = '';
  paciente = signal<Paciente | null>(null);
  registros = signal<RegistroAuditoria[]>([]);
  cargando = signal(true);
  error = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private historialService: HistorialClinicoService,
    private pacienteService: PacienteService
  ) {}

  async ngOnInit(): Promise<void> {
    this.pacienteId = this.route.snapshot.paramMap.get('pacienteId')!;
    this.cargando.set(true);
    try {
      const [pacientes, registros] = await Promise.all([
        this.pacienteService.getAll(undefined, true),
        this.historialService.getAuditoria(this.pacienteId)
      ]);
      this.paciente.set(pacientes.find((p) => p.id === this.pacienteId) ?? null);
      this.registros.set(registros);
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo cargar la auditoría.');
    } finally {
      this.cargando.set(false);
    }
  }

  nombreEntidad(entidad: string): string {
    return this.etiquetaEntidad[entidad] ?? entidad;
  }
}
