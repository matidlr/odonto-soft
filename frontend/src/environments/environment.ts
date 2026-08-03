// Config de desarrollo local. Angular usa este archivo por defecto (con
// `ng serve`); para producción se reemplaza por environment.production.ts
// vía fileReplacements en angular.json (ver configuración "production").
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000/api'
};
