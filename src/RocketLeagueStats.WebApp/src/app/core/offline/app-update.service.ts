import { Injectable, inject, signal } from '@angular/core';
import { APP_UPDATE_CLIENT } from './tokens';

/**
 * Angular adapter over AppUpdateClient. Exposes `updateAvailable` as a signal
 * and delegates `applyUpdate()` to the injected client.
 *
 * Inject this service somewhere that runs at startup (e.g. App component) to
 * begin listening for updates - it does not bootstrap itself.
 *
 * UI is the consumer's responsibility - bind `updateAvailable()` to a banner,
 * then call `applyUpdate()` on click.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly client = inject(APP_UPDATE_CLIENT);
  private readonly _updateAvailable = signal(false);

  readonly updateAvailable = this._updateAvailable.asReadonly();

  constructor() {
    this.client.addEventListener('update-available', () => {
      this._updateAvailable.set(true);
    });
  }

  /**
   * Posts SKIP_WAITING to the waiting SW, awaits controllerchange, then
   * reloads the page. Pass `{ reload: false }` if the app prefers manual
   * post-update navigation.
   */
  applyUpdate(options?: { reload?: boolean }): Promise<void> {
    return this.client.applyUpdate(options);
  }
}
