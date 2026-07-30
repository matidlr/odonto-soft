import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-registro',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './registro.component.html',
  styleUrl: './login.component.scss'
})
export class RegistroComponent {
  nombreClinica = '';
  slug = '';
  nombreOdontologo = '';
  email = '';
  password = '';
  matricula = '';
  especialidad = '';

  cargando = signal(false);
  error = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  async onSubmit(): Promise<void> {
    this.error.set(null);
    this.cargando.set(true);
    try {
      await this.auth.registrarOdontologo({
        nombreClinica: this.nombreClinica,
        slug: this.slug,
        nombreOdontologo: this.nombreOdontologo,
        email: this.email,
        password: this.password,
        matricula: this.matricula,
        especialidad: this.especialidad || undefined
      });
      // El registro no inicia sesión solo: logueamos con las mismas
      // credenciales que acaba de crear.
      await this.auth.login(this.email, this.password);
      this.router.navigateByUrl('/');
    } catch (err: unknown) {
      const httpError = err as { error?: { message?: string } };
      this.error.set(httpError?.error?.message ?? 'No se pudo completar el registro.');
    } finally {
      this.cargando.set(false);
    }
  }
}
