import { AfterViewInit, Component, ElementRef, NgZone, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { environment } from '../../../environments/environment';

// Google Identity Services se carga por <script> en index.html, no como
// paquete de npm — no hay tipos oficiales livianos para esto, así que
// declaramos nada más lo que usamos.
declare const google: {
  accounts: {
    id: {
      initialize(config: { client_id: string; callback: (resp: { credential: string }) => void }): void;
      renderButton(parent: HTMLElement, options: { theme: string; size: string; width?: number; text: string }): void;
    };
  };
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements AfterViewInit {
  email = '';
  password = '';
  cargando = signal(false);
  error = signal<string | null>(null);

  // Si llegamos acá porque InactividadService cerró la sesión sola, se
  // avisa (no es un error, así que va aparte del cartel de error rojo).
  mensajeInactividad = signal(false);

  // Si no hay Client ID configurado (environment.googleClientId vacío), no
  // mostramos el botón — evita un botón roto en ambientes donde todavía no
  // se configuró Google Cloud Console.
  mostrarBotonGoogle = !!environment.googleClientId;

  @ViewChild('googleBotonContainer') googleBotonContainer?: ElementRef<HTMLElement>;

  constructor(
    private auth: AuthService,
    private router: Router,
    private zone: NgZone,
    route: ActivatedRoute
  ) {
    this.mensajeInactividad.set(route.snapshot.queryParamMap.get('motivo') === 'inactividad');
  }

  ngAfterViewInit(): void {
    if (!this.mostrarBotonGoogle || !this.googleBotonContainer) return;

    // El script de Google es async/defer, así que puede no estar listo
    // todavía en este punto — reintentamos un par de veces en vez de fallar
    // en silencio.
    this.inicializarGoogleConReintento();
  }

  private inicializarGoogleConReintento(intentos = 0): void {
    if (typeof google === 'undefined' || !google?.accounts?.id) {
      if (intentos < 20) {
        setTimeout(() => this.inicializarGoogleConReintento(intentos + 1), 250);
      }
      return;
    }

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      // El callback de Google llega fuera de la zona de Angular; sin este
      // envoltorio, cambios como error() o la navegación no disparan
      // detección de cambios.
      callback: (resp) => this.zone.run(() => this.onGoogleCredential(resp.credential))
    });

    google.accounts.id.renderButton(this.googleBotonContainer!.nativeElement, {
      theme: 'outline',
      size: 'large',
      text: 'signin_with'
    });
  }

  private async onGoogleCredential(idToken: string): Promise<void> {
    this.error.set(null);
    this.cargando.set(true);
    try {
      await this.auth.loginConGoogle(idToken);
      this.router.navigateByUrl('/');
    } catch {
      this.error.set('No pudimos iniciar sesión con esa cuenta de Google. ¿Ya te registraste con este email?');
    } finally {
      this.cargando.set(false);
    }
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
