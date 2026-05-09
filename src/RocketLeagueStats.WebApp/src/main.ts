import { isDevMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

bootstrapApplication(App, appConfig)
  .then(() => {
    // Skip registration during `ng serve` so HMR and proxy hot reload aren't
    // shadowed by the SW. The SW only ships in production builds anyway -
    // build-sw.mjs runs after `ng build` and never during the dev server.
    if (!isDevMode() && 'serviceWorker' in navigator) {
      void navigator.serviceWorker.register('/sw.js', { scope: '/' });
    }
  })
  .catch((err) => console.error(err));
