import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

// Layout del panel autenticado: navbar fija a la izquierda + contenido
// de la ruta activa a la derecha. Los links que ve cada quien podrían
// filtrarse por rol más adelante (por ahora todos ven todo).
@Component({
  selector: 'app-panel-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './panel-layout.component.html',
  styleUrl: './panel-layout.component.scss'
})
export class PanelLayoutComponent {
  constructor(public auth: AuthService) {}
}
