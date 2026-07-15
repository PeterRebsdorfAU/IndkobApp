// Produktion (Render): peg på den udrullede backend-URL.
// VIGTIGT: Ret denne til DIN faktiske Render-API-URL hvis den afviger
// (Render viser den efter første deploy, fx https://indkobapp-api.onrender.com).
export const environment = {
  production: true,
  apiBase: 'https://indkobapp.onrender.com/api',
  // Fejllogning & overvågning (T6). Tom => Sentry slås fra (no-op). Indsæt husstandens/projektets
  // frontend-DSN her ved deploy for at aktivere fejl-rapportering i produktion.
  sentryDsn: ''
};
