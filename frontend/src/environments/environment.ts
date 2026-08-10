// Config de desarrollo local. Angular usa este archivo por defecto (con
// `ng serve`); para producción se reemplaza por environment.production.ts
// vía fileReplacements en angular.json (ver configuración "production").
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api/v1',
  // Client ID de OAuth (tipo "Web application") creado en Google Cloud
  // Console. No es secreto — viaja al navegador igual, por eso puede vivir
  // acá. Dejalo vacío y el botón de Google simplemente no aparece.
  googleClientId: ''
};
