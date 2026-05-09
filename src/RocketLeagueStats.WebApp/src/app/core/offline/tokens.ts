import { InjectionToken } from '@angular/core';
import { AppUpdateClient } from './app-update-client';

/**
 * Default factory provides a real client wired to navigator.serviceWorker.
 * Override in tests via:
 *   { provide: APP_UPDATE_CLIENT, useFactory: () => new StubClient() as any }
 */
export const APP_UPDATE_CLIENT = new InjectionToken<AppUpdateClient>(
  'APP_UPDATE_CLIENT',
  {
    providedIn: 'root',
    factory: () => {
      // Guard the SSR/test path: the client constructor reaches for
      // navigator.serviceWorker. Returning a no-op EventTarget keeps DI valid
      // when the API is absent (jsdom, Node, very old browsers).
      if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) {
        return new EventTarget() as unknown as AppUpdateClient;
      }
      return new AppUpdateClient();
    },
  },
);
