import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Endpoints que nunca deben disparar un intento de refresh (para no entrar
// en loop, o porque no tiene sentido: login/registro/recuperación son
// anónimos, y refresh/logout son parte del propio mecanismo de sesión).
const RUTAS_SIN_REFRESH = [
  '/auth/login',
  '/auth/refresh',
  '/auth/logout',
  '/auth/registrar-odontologo',
  '/auth/bootstrap-superadmin',
  '/auth/reset-superadmin-password',
  '/auth/olvide-password',
  '/auth/resetear-password'
];

// Si dos pedidos fallan con 401 al mismo tiempo (por ejemplo, la pantalla
// dispara varios llamados juntos), comparten el mismo refresh en curso en
// vez de pedir uno cada uno.
let refrescoEnCurso: Promise<string | null> | null = null;

// El access token dura solo 20 minutos. Cuando vence, el backend devuelve
// 401 — en vez de mandar al usuario derecho al login, probamos renovarlo en
// silencio con el refresh token (va en una cookie httpOnly, no lo tocamos
// acá) y reintentamos el pedido original una sola vez.
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  if (RUTAS_SIN_REFRESH.some((ruta) => req.url.includes(ruta)) || !auth.sesion()) {
    return next(req);
  }

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) {
        return throwError(() => err);
      }

      if (!refrescoEnCurso) {
        refrescoEnCurso = auth.refrescarToken().finally(() => {
          refrescoEnCurso = null;
        });
      }

      return from(refrescoEnCurso).pipe(
        switchMap((nuevoToken) => {
          if (!nuevoToken) {
            auth.logout();
            return throwError(() => err);
          }
          const reintento = req.clone({ setHeaders: { Authorization: `Bearer ${nuevoToken}` } });
          return next(reintento);
        })
      );
    })
  );
};
