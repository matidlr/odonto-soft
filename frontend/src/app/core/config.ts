import { environment } from '../../environments/environment';

// La URL de la API sale de environments/environment.ts (dev) o
// environments/environment.production.ts (build de producción, ver
// fileReplacements en angular.json). Así, un build de producción nunca
// puede terminar apuntando a localhost por accidente.
export const API_BASE_URL = environment.apiBaseUrl;
