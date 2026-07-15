import { bootstrapApplication } from '@angular/platform-browser';
import * as Sentry from '@sentry/angular';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

// Fejllogning & overvågning (T6). Sentry initialiseres KUN når en DSN er sat (env/environment).
// Uden DSN gøres intet (no-op) — appen kører uændret, ingen netværkskald, ingen crash. Selve
// @sentry/angular-pakken bundles med appen (self-hostet), så PWA'en har ingen CDN-afhængighed.
if (environment.sentryDsn) {
  Sentry.init({
    dsn: environment.sentryDsn,
    environment: environment.production ? 'production' : 'development',
    // Konservativ tracing-sampling: 100% i dev, 20% i prod.
    tracesSampleRate: environment.production ? 0.2 : 1.0,
    sendDefaultPii: false
  });
}

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
