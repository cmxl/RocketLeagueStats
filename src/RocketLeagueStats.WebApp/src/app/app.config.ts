import { ApplicationConfig, provideAppInitializer, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimationsAsync(),
    // Persistent storage prevents the precache + API cache from being evicted
    // under storage pressure. Browsers grant or deny based on engagement
    // signals (PWA installed, frequent use); the call is silent when denied
    // and is window-only - never call it from the SW.
    provideAppInitializer(async () => {
      if (typeof navigator !== 'undefined' && 'storage' in navigator && 'persist' in navigator.storage) {
        await navigator.storage.persist();
      }
    }),
  ],
};
