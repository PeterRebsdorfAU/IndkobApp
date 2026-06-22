// Produktion (Render): peg på den udrullede backend-URL.
// VIGTIGT: Ret denne til DIN faktiske Render-API-URL hvis den afviger
// (Render viser den efter første deploy, fx https://indkobapp-api.onrender.com).
export const environment = {
  production: true,
  apiBase: 'https://indkobapp-api.onrender.com/api'
};
