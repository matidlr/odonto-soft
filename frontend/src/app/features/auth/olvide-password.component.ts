import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-olvide-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './olvide-password.component.html',
  styleUrl: './olvide-password.component.scss'
})
export class OlvidePasswordComponent {
  email = '';
  cargando = signal(false);
  enviado = signal(false);
  error = signal<string | null>(null);

  constructor(private auth: AuthService) {}

  async onSubmit(): Promise<void> {
    this.error.set(null);
    this.cargando.set(true);
    try {
      await this.auth.olvidePassword(this.email);
      this.enviado.set(true);
    } catch {
      this.error.set('No pudimos procesar el pedido. Probá de nuevo en un momento.');
    } finally {
      this.cargando.set(false);
    }
  }
}
