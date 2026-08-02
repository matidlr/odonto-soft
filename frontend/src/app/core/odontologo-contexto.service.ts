import { Injectable, computed, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { Odontologo, OdontologoService } from './odontologo.service';

const STORAGE_PREFIX = 'odonto_odontologo_seleccionado_';

// La clínica tiene un solo login compartido; este servicio guarda "qué
// odontólogo estoy usando ahora" (elegido en el selector arriba del menú)
// para que Agenda, Pacientes, etc. puedan filtrar por él. Se persiste en
// localStorage por tenant, para no arrastrar la selección de una clínica a
// otra si alguna vez se prueban varias cuentas desde el mismo navegador.
@Injectable({ providedIn: 'root' })
export class OdontologoContextoService {
  odontologos = signal<Odontologo[]>([]);
  seleccionadoId = signal<string | null>(null);
  cargado = signal(false);

  seleccionado = computed(
    () => this.odontologos().find((o) => o.id === this.seleccionadoId()) ?? null
  );
  hayMasDeUno = computed(() => this.odontologos().length > 1);

  constructor(
    private odontologoService: OdontologoService,
    private auth: AuthService
  ) {}

  async cargar(): Promise<void> {
    const lista = await this.odontologoService.getAll();
    this.odontologos.set(lista);

    const guardado = localStorage.getItem(this.storageKey());
    const idValido = lista.find((o) => o.id === guardado);
    this.seleccionadoId.set(idValido ? guardado! : (lista[0]?.id ?? null));
    this.cargado.set(true);
  }

  seleccionar(id: string): void {
    this.seleccionadoId.set(id);
    localStorage.setItem(this.storageKey(), id);
  }

  private storageKey(): string {
    return STORAGE_PREFIX + (this.auth.sesion()?.tenantId ?? 'sin-tenant');
  }
}
