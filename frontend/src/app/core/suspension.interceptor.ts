import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

// Si el backend devuelve 403 en un endpoint que exige "TenantActivo", es
// porque se venció la prueba (o la suspendió el SuperAdmin) — en vez de
// dejar que cada pantalla muestre su propio error genérico, mandamos
// derecho a la pantalla de Plan con el aviso correspondiente.
export const suspensionInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 403 && !router.url.startsWith('/plan')) {
        router.navigate(['/plan'], { queryParams: { motivo: 'suspendido' } });
      }
      return throwError(() => err);
    })
  );
};
