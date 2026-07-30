import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

// Le pega el JWT (si hay sesión) a todos los requests que salen hacia
// nuestra propia API. No hace falta filtrar por URL todavía porque
// por ahora el frontend solo le habla a nuestro backend.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.sesion()?.token;

  if (!token) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    })
  );
};
