import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { AppUpdateService } from '../../core/offline/app-update.service';
import { PanelComponent } from './panel.component';

@Component({
  selector: 'rls-app-update-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PanelComponent],
  template: `
    @if (visible()) {
      <div class="update-container" role="status" aria-live="polite">
        <rls-panel team="neutral">
          <div class="update-content">
            <h3 class="update-title">Update Ready</h3>
            <p class="update-message">A new version of the dashboard is available. Reload to apply.</p>
            <div class="update-actions">
              <button type="button" (click)="apply()" class="btn-apply" [disabled]="applying()">
                {{ applying() ? 'Reloading…' : 'Reload Now' }}
              </button>
              <button type="button" (click)="dismiss()" class="btn-dismiss" [disabled]="applying()">
                Later
              </button>
            </div>
          </div>
        </rls-panel>
      </div>
    }
  `,
  styles: [`
    .update-container {
      position: fixed;
      bottom: 1.5rem;
      left: 1.5rem;
      z-index: 500;
      max-width: 320px;
      animation: rls-fade-in 240ms ease;
    }
    .update-content {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .update-title {
      font-family: var(--font-header);
      font-size: var(--text-sm);
      color: var(--accent-cyan);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin: 0;
    }
    .update-message {
      font-family: var(--font-body);
      font-size: var(--text-sm);
      color: var(--text-secondary);
      margin: 0;
      line-height: 1.4;
    }
    .update-actions {
      display: flex;
      gap: 0.75rem;
      margin-top: 0.5rem;
    }
    .btn-apply {
      padding: 0.375rem 1rem;
      background: var(--accent-cyan);
      color: var(--bg-base);
      border: 0;
      border-radius: 2px;
      font-family: var(--font-header);
      font-weight: 700;
      font-size: var(--text-sm);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      cursor: pointer;
    }
    .btn-apply:hover:not(:disabled) { filter: brightness(1.15); }
    .btn-apply:disabled { opacity: 0.6; cursor: progress; }
    .btn-dismiss {
      padding: 0.375rem 1rem;
      background: transparent;
      color: var(--text-secondary);
      border: 1px solid var(--text-muted);
      border-radius: 2px;
      font-family: var(--font-header);
      font-weight: 600;
      font-size: var(--text-sm);
      text-transform: uppercase;
      cursor: pointer;
    }
    .btn-dismiss:hover:not(:disabled) { color: var(--text-primary); }
    .btn-dismiss:disabled { opacity: 0.6; }
  `],
})
export class AppUpdateBannerComponent {
  private readonly updates = inject(AppUpdateService);
  private readonly _dismissed = signal(false);
  protected readonly applying = signal(false);

  // Hidden until the service flips updateAvailable, and stays hidden once
  // the user explicitly dismisses for this session. The next page reload
  // re-detects a still-waiting SW and the banner returns - that's the
  // intended behaviour: dismiss is "not now", not "never".
  protected readonly visible = computed(
    () => this.updates.updateAvailable() && !this._dismissed(),
  );

  protected dismiss(): void {
    this._dismissed.set(true);
  }

  protected async apply(): Promise<void> {
    this.applying.set(true);
    try {
      // On success the SW activates, fires controllerchange, and the client
      // calls location.reload() - this component will be torn down before
      // the await resolves. If we land in catch (10s controllerchange
      // timeout, or no waiting worker) re-enable the buttons so the user
      // can dismiss or retry.
      await this.updates.applyUpdate();
    } catch {
      this.applying.set(false);
    }
  }
}
