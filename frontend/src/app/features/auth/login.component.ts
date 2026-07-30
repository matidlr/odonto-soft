import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
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

  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

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
