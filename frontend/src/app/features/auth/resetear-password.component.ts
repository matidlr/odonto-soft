import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-resetear-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './resetear-password.component.html',
  styleUrl: './resetear-password.component.scss'
})
export class ResetearPasswordComponent implements OnInit {
  token = '';
  password = '';
  confirmarPassword = '';
  cargando = signal(false);
  listo = signal(false);
  error = signal<string | null>(null);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) {
      this.error.set('El enlace no es válido. Pedí uno nuevo desde "¿Olvidaste tu contraseña?".');
    }
  }

  async onSubmit(): Promise<void> {
    this.error.set(null);

    if (this.password.length < 8) {
      this.error.set('La contraseña debe tener al menos 8 caracteres.');
      return;
    }
    if (this.password !== this.confirmarPassword) {
      this.error.set('Las contraseñas no coinciden.');
      return;
    }

    this.cargando.set(true);
    try {
      await this.auth.resetearPassword(this.token, this.password);
      this.listo.set(true);
    } catch (e: any) {
      const mensaje = e?.error?.message ?? 'El enlace es inválido o venció. Pedí uno nuevo.';
      this.error.set(mensaje);
    } finally {
      this.cargando.set(false);
    }
  }

  irALogin(): void {
    this.router.navigateByUrl('/login');
  }
}
