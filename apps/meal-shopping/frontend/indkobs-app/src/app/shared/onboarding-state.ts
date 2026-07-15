// Første-gangs-onboarding: vi husker i localStorage om guiden er set, så nye
// brugere kun møder den én gang. Nøglen ligger her (ét sted) så app.ts,
// onboarding-siden og FAQ-siden er enige. Bevidst uden backend — ren klient-tilstand.
const ONBOARDING_KEY = 'indkobs.onboarding.v1';

/** Har brugeren afsluttet (eller sprunget over) onboarding på denne enhed? */
export function hasSeenOnboarding(): boolean {
  try {
    return localStorage.getItem(ONBOARDING_KEY) === '1';
  } catch {
    // Privat browsing o.l. hvor localStorage kaster → vis ikke guiden i en løkke.
    return true;
  }
}

/** Markér onboarding som set, så den ikke vises igen automatisk. */
export function markOnboardingSeen(): void {
  try {
    localStorage.setItem(ONBOARDING_KEY, '1');
  } catch {
    /* ignorér — ikke kritisk */
  }
}

/** Nulstil, så guiden vises igen (bruges af "Se introduktionen igen" i FAQ). */
export function resetOnboarding(): void {
  try {
    localStorage.removeItem(ONBOARDING_KEY);
  } catch {
    /* ignorér */
  }
}
