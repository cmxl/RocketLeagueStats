import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SettingsStore } from '../../core/state/settings.store';

@Component({
  selector: 'rls-settings-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule],
  template: `
    <div class="settings-page">
      <h1 class="settings-page__title">Settings</h1>
      <form class="settings-form" (ngSubmit)="store.save()">
        <div class="settings-group">
          <label class="settings-label" for="playerName">
            Your in-game name
            <span class="settings-hint">Used to highlight your stats across all views</span>
          </label>
          <input
            id="playerName"
            class="settings-input"
            type="text"
            name="playerName"
            [ngModel]="store.current().playerName"
            (ngModelChange)="store.setDraft({ playerName: $event })"
            placeholder="Enter your Rocket League name…" />
        </div>

        <div class="settings-group settings-group--inline">
          <label class="settings-label" for="showTraining">
            Show training matches in history
          </label>
          <input
            id="showTraining"
            class="settings-checkbox"
            type="checkbox"
            name="showTraining"
            [ngModel]="store.current().showTrainingInHistory"
            (ngModelChange)="store.setDraft({ showTrainingInHistory: $event })" />
        </div>

        <div class="settings-actions">
          <button
            type="submit"
            class="btn btn--primary"
            [disabled]="!store.hasUnsavedChanges()">
            Save
          </button>
          <button
            type="button"
            class="btn btn--secondary"
            (click)="store.cancel()"
            [disabled]="!store.hasUnsavedChanges()">
            Cancel
          </button>
          @if (store.saveStatus() === 'saving') {
            <span class="settings-status">Saving…</span>
          } @else if (store.saveStatus() === 'error') {
            <span class="settings-status settings-status--error">Save failed</span>
          }
        </div>
      </form>
    </div>
  `,
  styles: [`
    .settings-page { padding: 2rem 1.5rem; max-width: 600px; margin: 0 auto; }
    .settings-page__title { font-family: var(--font-display); font-size: var(--text-display-md); color: var(--text-primary); margin: 0 0 2rem; letter-spacing: 0.04em; }
    .settings-form { display: flex; flex-direction: column; gap: 1.5rem; }
    .settings-group { display: flex; flex-direction: column; gap: 0.375rem; }
    .settings-group--inline { flex-direction: row; align-items: center; justify-content: space-between; }
    .settings-label { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-primary); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; display: flex; flex-direction: column; gap: 0.2rem; }
    .settings-hint { font-family: var(--font-body); font-size: var(--text-xs); color: var(--text-muted); text-transform: none; letter-spacing: 0; font-weight: 400; }
    .settings-input {
      background: var(--bg-elevated);
      border: 1px solid var(--text-muted);
      color: var(--text-primary);
      font-family: var(--font-body);
      font-size: var(--text-sm);
      padding: 0.5rem 0.75rem;
      border-radius: 4px;
      transition: border-color 150ms;
    }
    .settings-input:focus { outline: none; border-color: var(--accent-cyan); }
    .settings-input::placeholder { color: var(--text-muted); }
    .settings-checkbox { width: 1.25rem; height: 1.25rem; accent-color: var(--accent-cyan); cursor: pointer; }
    .settings-actions { display: flex; align-items: center; gap: 0.75rem; padding-top: 0.5rem; }
    .btn { padding: 0.5rem 1.25rem; border-radius: 4px; border: none; font-family: var(--font-header); font-size: var(--text-sm); font-weight: 700; text-transform: uppercase; letter-spacing: 0.05em; cursor: pointer; transition: opacity 150ms; }
    .btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .btn--primary { background: var(--accent-cyan); color: var(--bg-base); }
    .btn--primary:not(:disabled):hover { opacity: 0.85; }
    .btn--secondary { background: var(--bg-elevated); color: var(--text-secondary); border: 1px solid var(--text-muted); }
    .btn--secondary:not(:disabled):hover { border-color: var(--accent-cyan); color: var(--text-primary); }
    .settings-status { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-secondary); }
    .settings-status--error { color: var(--accent-danger); }
  `],
})
export class SettingsPageComponent {
  protected readonly store = inject(SettingsStore);
}
