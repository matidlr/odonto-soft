// Config de producción. Reemplaza a environment.ts en un build con
// `ng build --configuration production` (fileReplacements en angular.json).
//
// IMPORTANTE: cuando despliegues de verdad, cambiá apiBaseUrl por la URL
// real de tu API — tiene que empezar con "https://" sí o sí. Si la dejás
// en http:// (o con este placeholder), la app no arranca: hay un chequeo
// en main.ts pensado justamente para que no se te pase por alto.
export const environment = {
  production: true,
  apiBaseUrl: 'https://CAMBIAR-ANTES-DE-DESPLEGAR.example.com/api'
};
