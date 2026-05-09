// Framework-agnostic client that detects when a new service worker is waiting
// to activate, exposes that as an 'update-available' event, and applies the
// update on demand. The Angular adapter (AppUpdateService) wraps it.
//
// Update flow contract (matches src/sw.ts):
//   1. Browser detects new sw.js, installs it -> 'installing' state.
//   2. New SW transitions 'installing' -> 'installed' while the old SW still
//      controls the page. This client emits 'update-available' here.
//   3. Host UI shows a toast/banner bound to updateAvailable state.
//   4. User clicks "apply" -> client posts SKIP_WAITING to the waiting worker.
//   5. New SW activates, fires controllerchange. Client emits 'update-applied'
//      and reloads (unless caller opts out).

import type { ClientMessage } from '../../../sw-protocol';

export interface AppUpdateClientEventMap {
  'update-available': CustomEvent<void>;
  'update-applied': CustomEvent<void>;
}

export class AppUpdateClient extends EventTarget {
  private pollHandle: ReturnType<typeof setInterval> | null = null;
  private updateAvailable = false;
  private registration: ServiceWorkerRegistration | null = null;
  private disposed = false;

  private readonly onUpdateFound = (): void => {
    const reg = this.registration;
    const installing = reg?.installing;
    if (!installing) return;
    installing.addEventListener('statechange', () => {
      if (installing.state === 'installed' && this.container.controller) this.signalUpdate();
    });
  };
  private readonly onVisibilityChange = (): void => {
    if (this.disposed) return;
    if (document.visibilityState === 'visible') this.startPolling();
    else this.stopPolling();
  };

  /**
   * @param container Defaults to navigator.serviceWorker. Pass a stub for tests.
   * @param pollIntervalMs How often to call `registration.update()` while the
   *   page is foreground. Default 1 hour. Set to 0 to disable polling.
   */
  constructor(
    private readonly container: ServiceWorkerContainer = navigator.serviceWorker,
    private readonly pollIntervalMs: number = 60 * 60 * 1000,
  ) {
    super();
    void this.init();
  }

  get hasUpdate(): boolean { return this.updateAvailable; }

  /**
   * Posts SKIP_WAITING to the waiting worker, awaits controllerchange (with
   * a 10s timeout), then reloads. No-op if no worker is waiting.
   */
  async applyUpdate(options?: { reload?: boolean }): Promise<void> {
    const reg = await this.container.getRegistration();
    if (!reg?.waiting) return;

    const controllerChange = this.awaitControllerChange();
    const skipWaiting: ClientMessage = { type: 'SKIP_WAITING' };
    reg.waiting.postMessage(skipWaiting);
    await controllerChange;

    this.dispatchEvent(new CustomEvent('update-applied'));
    if (options?.reload !== false) location.reload();
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.stopPolling();
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    this.registration?.removeEventListener('updatefound', this.onUpdateFound);
  }

  private awaitControllerChange(timeoutMs = 10_000): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const onChange = (): void => {
        clearTimeout(timer);
        resolve();
      };
      const timer = setTimeout(() => {
        this.container.removeEventListener('controllerchange', onChange);
        reject(new Error(`Service worker controllerchange timed out after ${timeoutMs}ms`));
      }, timeoutMs);
      this.container.addEventListener('controllerchange', onChange, { once: true });
    });
  }

  private async init(): Promise<void> {
    if (!this.container) return;
    const reg = await this.container.ready.catch(() => null);
    if (this.disposed || !reg) return;
    this.registration = reg;

    if (reg.waiting && this.container.controller) this.signalUpdate();

    reg.addEventListener('updatefound', this.onUpdateFound);

    if (this.pollIntervalMs > 0) {
      document.addEventListener('visibilitychange', this.onVisibilityChange);
      if (document.visibilityState === 'visible') this.startPolling();
    }
  }

  private startPolling(): void {
    this.stopPolling();
    if (!this.registration || this.pollIntervalMs <= 0) return;
    const reg = this.registration;
    this.pollHandle = setInterval(() => { void reg.update(); }, this.pollIntervalMs);
  }

  private stopPolling(): void {
    if (this.pollHandle != null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
  }

  private signalUpdate(): void {
    if (this.updateAvailable) return;
    this.updateAvailable = true;
    this.dispatchEvent(new CustomEvent('update-available'));
  }

  override addEventListener<K extends keyof AppUpdateClientEventMap>(
    type: K,
    listener: (this: AppUpdateClient, ev: AppUpdateClientEventMap[K]) => void,
    options?: boolean | AddEventListenerOptions,
  ): void;
  override addEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject | null,
    options?: boolean | AddEventListenerOptions,
  ): void;
  override addEventListener(type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | AddEventListenerOptions): void {
    super.addEventListener(type, listener, options);
  }

  override removeEventListener<K extends keyof AppUpdateClientEventMap>(
    type: K,
    listener: (this: AppUpdateClient, ev: AppUpdateClientEventMap[K]) => void,
    options?: boolean | EventListenerOptions,
  ): void;
  override removeEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject | null,
    options?: boolean | EventListenerOptions,
  ): void;
  override removeEventListener(type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | EventListenerOptions): void {
    super.removeEventListener(type, listener, options);
  }
}
