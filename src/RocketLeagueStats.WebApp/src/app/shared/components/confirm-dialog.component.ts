import { Component, ChangeDetectionStrategy, input, output, HostListener } from '@angular/core';
import { PanelComponent } from './panel.component';

/**
 * Reusable confirmation dialog. Renders a backdrop + clipped panel matching the rest of the
 * app's dark/cyan/sharp-edge style. Parent owns the open/close lifecycle (`@if (show) { ... }`)
 * and reacts to (confirm) / (cancel) outputs. ESC and backdrop-click both fire (cancel).
 */
@Component({
  selector: 'rls-confirm-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PanelComponent],
  template: `
    <div class="dialog-backdrop" (click)="cancel.emit()" role="presentation"></div>
    <div class="dialog-container" role="dialog" aria-modal="true" [attr.aria-label]="title()">
      <rls-panel [team]="variant() === 'danger' ? 'orange' : 'neutral'">
        <h2 class="dialog-title">{{ title() }}</h2>
        <p class="dialog-message">{{ message() }}</p>
        <div class="dialog-actions">
          <button type="button" class="btn btn--ghost" (click)="cancel.emit()">
            {{ cancelLabel() }}
          </button>
          <button type="button" class="btn"
                  [class.btn--danger]="variant() === 'danger'"
                  [class.btn--primary]="variant() !== 'danger'"
                  (click)="confirm.emit()">
            {{ confirmLabel() }}
          </button>
        </div>
      </rls-panel>
    </div>
  `,
  styles: [`
    /* Fullscreen overlay sits above everything else; backdrop dims the page so the dialog draws
       focus. position:fixed inset:0 is the standard modal pattern — we lean on z-index 2000 to
       stay above connection-banner (1000) and toast (500). */
    .dialog-backdrop {
      position: fixed;
      inset: 0;
      background: var(--bg-overlay);
      backdrop-filter: blur(2px);
      z-index: 2000;
      animation: rls-fade-in 150ms ease;
    }
    .dialog-container {
      position: fixed;
      inset: 0;
      z-index: 2001;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1.5rem;
      pointer-events: none;
    }
    .dialog-container > * { pointer-events: auto; min-width: 320px; max-width: 480px; }

    .dialog-title {
      font-family: var(--font-header);
      font-size: var(--text-lg);
      color: var(--text-primary);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin: 0 0 0.75rem;
    }
    .dialog-message {
      font-family: var(--font-body);
      font-size: var(--text-sm);
      color: var(--text-secondary);
      line-height: 1.5;
      margin: 0 0 1.25rem;
    }
    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
    .btn {
      padding: 0.5rem 1.25rem;
      border-radius: 2px;
      border: 1px solid transparent;
      font-family: var(--font-header);
      font-weight: 700;
      font-size: var(--text-sm);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      cursor: pointer;
      transition: opacity 120ms, background 120ms;
    }
    .btn:hover { opacity: 0.85; }
    .btn--ghost {
      background: transparent;
      border-color: var(--text-muted);
      color: var(--text-secondary);
    }
    .btn--ghost:hover { color: var(--text-primary); border-color: var(--text-secondary); }
    .btn--danger {
      background: var(--accent-danger);
      color: var(--bg-base);
    }
    .btn--primary {
      background: var(--accent-cyan);
      color: var(--bg-base);
    }
  `],
})
export class ConfirmDialogComponent {
  readonly title = input<string>('Are you sure?');
  readonly message = input<string>('This action cannot be undone.');
  readonly confirmLabel = input<string>('Confirm');
  readonly cancelLabel = input<string>('Cancel');
  readonly variant = input<'danger' | 'primary'>('danger');

  readonly confirm = output<void>();
  readonly cancel = output<void>();

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.cancel.emit();
  }
}
