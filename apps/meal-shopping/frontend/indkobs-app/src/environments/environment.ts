// Udvikling: tom apiBase => api.ts falder tilbage til samme vært som siden hentes
// fra (localhost på PC, PC'ens LAN-IP på telefon). Se src/app/api.ts.
export const environment = {
  production: false,
  apiBase: '',
  // Fejllogning & overvågning (T6). Tom => Sentry slås fra (no-op). Sæt en DSN her lokalt for at
  // teste fejl-rapportering i dev; i prod injiceres den ved build (se environment.prod.ts).
  sentryDsn: ''
};
