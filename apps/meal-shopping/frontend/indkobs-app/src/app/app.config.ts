import { ApplicationConfig, ErrorHandler, Provider, provideBrowserGlobalErrorListeners, provideZoneChangeDetection, isDevMode } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import * as Sentry from '@sentry/angular';

import { routes } from './app.routes';
import { authInterceptor } from './auth-interceptor';
import { provideServiceWorker } from '@angular/service-worker';
import { environment } from '../environments/environment';

// Fejllogning & overvågning (T6): send uhåndterede fejl til Sentry — men KUN når en DSN er sat.
// Uden DSN tilføjes intet, og Angulars standard-fejlhåndtering bevares (no-op).
const sentryProviders: Provider[] = environment.sentryDsn
  ? [{ provide: ErrorHandler, useValue: Sentry.createErrorHandler() }]
  : [];

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])), provideServiceWorker('ngsw-worker.js', {
            enabled: !isDevMode(),
            registrationStrategy: 'registerWhenStable:30000'
          }),
    ...sentryProviders
  ]
};
