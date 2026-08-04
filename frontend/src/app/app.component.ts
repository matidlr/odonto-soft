import { Component, effect } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { InactividadService } from './core/inactividad.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'frontend';

  constructor(
    protected auth: AuthService,
    protected inactividad: InactividadService
  ) {
    // Solo se controla la inactividad mientras hay una sesión iniciada; al
    // hacer login se prende, al hacer logout se apaga (no tiene sentido
    // medir inactividad en la pantalla de login).
    effect(() => {
      if (this.auth.estaLogueado()) {
        this.inactividad.iniciar();
      } else {
        this.inactividad.detener();
      }
    });
  }
}
