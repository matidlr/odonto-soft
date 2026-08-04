import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  email = '';
  password = '';
  cargando = signal(false);
  error = signal<string | null>(null);

  // Si llegamos acá porque InactividadService cerró la sesión sola, se
  // avisa (no es un error, así que va aparte del cartel de error rojo).
  mensajeInactividad = signal(false);

  constructor(
    private auth: AuthService,
    private router: Router,
    route: ActivatedRoute
  ) {
    this.mensajeInactividad.set(route.snapshot.queryParamMap.get('motivo') === 'inactividad');
  }

  async onSubmit(): Promise<void> {
    this.error.set(null);
    this.cargando.set(true);
    try {
      await this.auth.login(this.email, this.password);
      this.router.navigateByUrl('/');
    } catch {
      this.error.set('Email o contraseña incorrectos.');
    } finally {
      this.cargando.set(false);
    }
  }
}
