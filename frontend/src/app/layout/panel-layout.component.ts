import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { OdontologoContextoService } from '../core/odontologo-contexto.service';

// Layout del panel autenticado: navbar fija a la izquierda + contenido
// de la ruta activa a la derecha. Los links que ve cada quien podrían
// filtrarse por rol más adelante (por ahora todos ven todo).
@Component({
  selector: 'app-panel-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './panel-layout.component.html',
  styleUrl: './panel-layout.component.scss'
})
export class PanelLayoutComponent implements OnInit {
  constructor(
    public auth: AuthService,
    public contexto: OdontologoContextoService
  ) {}

  async ngOnInit(): Promise<void> {
    // El SuperAdmin no tiene tenant ni odontólogos propios.
    if (this.auth.sesion()?.tenantId) {
      await this.contexto.cargar();
    }
  }
}
