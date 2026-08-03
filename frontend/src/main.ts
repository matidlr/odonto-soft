import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

// Nunca en producción con la API por http:// (ni con el placeholder que
// queda en environment.production.ts hasta que lo cambiés por la URL
// real). Frena la app acá, antes de arrancarla, en vez de dejar que
// alguien navegue una app "rota" a medias sobre una conexión insegura.
if (environment.production && !environment.apiBaseUrl.startsWith('https://')) {
  document.body.innerHTML =
    '<div style="font-family: sans-serif; padding: 2rem; color: #b91c1c;">' +
    'Config de producción inválida: apiBaseUrl tiene que empezar con https://. ' +
    'Revisá src/environments/environment.production.ts.</div>';
  throw new Error('apiBaseUrl de producción no es https://, se frena el arranque.');
}

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
