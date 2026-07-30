import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// Bloquea el acceso a rutas protegidas si no hay sesión iniciada.
// No mira roles ni estado del tenant acá (eso lo maneja cada pantalla,
// porque el candado real de negocio ya está en el backend con la policy
// TenantActivo — esto es solo para no mostrar UI vacía/rota).
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.estaLogueado()) {
    return true;
  }

  router.navigateByUrl('/login');
  return false;
};
