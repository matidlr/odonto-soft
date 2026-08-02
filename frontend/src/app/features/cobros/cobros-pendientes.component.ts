import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CobroService, PacientePendiente } from '../../core/cobro.service';

@Component({
  selector: 'app-cobros-pendientes',
  standalone: true,
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './cobros-pendientes.component.html',
  styleUrl: './cobros-pendientes.component.scss'
})
export class CobrosPendientesComponent implements OnInit {
  pendientes = signal<PacientePendiente[]>([]);
  cargando = signal(true);

  constructor(private cobroService: CobroService) {}

  async ngOnInit(): Promise<void> {
    this.cargando.set(true);
    try {
      this.pendientes.set(await this.cobroService.getPendientes());
    } finally {
      this.cargando.set(false);
    }
  }
}
