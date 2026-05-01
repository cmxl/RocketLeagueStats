import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastStore } from '../../core/state/toast.store';
import { PanelComponent } from './panel.component';

@Component({
  selector: 'rls-match-end-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PanelComponent],
  template: `
    @let summary = toast.matchEndedToast();
    @if (summary) {
      <div class="toast-container">
        <rls-panel team="mvp" [glow]="true">
          <div class="toast-content">
            <h3 class="toast-title">Match Ended</h3>
            <p class="toast-score">
              <span class="blue">{{ summary.blueScore }}</span>
              <span class="separator">—</span>
              <span class="orange">{{ summary.orangeScore }}</span>
            </p>
            <p class="toast-type">{{ summary.type }}</p>
            <div class="toast-actions">
              <a [routerLink]="['/recap', summary.matchId]" class="btn-recap">View Recap</a>
              <button (click)="toast.dismiss()" class="btn-dismiss">Dismiss</button>
            </div>
          </div>
        </rls-panel>
      </div>
    }
  `,
  styles: [`
    .toast-container {
      position: fixed;
      bottom: 1.5rem;
      right: 1.5rem;
      z-index: 500;
      min-width: 280px;
      animation: rls-slide-down 400ms ease;
    }
    .toast-content {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .toast-title {
      font-family: var(--font-header);
      font-size: var(--text-sm);
      color: var(--text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      margin: 0;
    }
    .toast-score {
      font-family: var(--font-display);
      font-size: var(--text-3xl);
      margin: 0;
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .blue  { color: var(--team-blue); }
    .orange { color: var(--team-orange); }
    .separator { color: var(--text-muted); font-size: var(--text-xl); }
    .toast-type {
      font-family: var(--font-header);
      font-size: var(--text-xs);
      color: var(--accent-mvp);
      text-transform: uppercase;
      margin: 0;
    }
    .toast-actions {
      display: flex;
      gap: 0.75rem;
      margin-top: 0.5rem;
    }
    .btn-recap {
      padding: 0.375rem 1rem;
      background: var(--accent-mvp);
      color: var(--bg-base);
      border-radius: 2px;
      font-family: var(--font-header);
      font-weight: 700;
      font-size: var(--text-sm);
      text-decoration: none;
      text-transform: uppercase;
    }
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
    .btn-dismiss:hover { color: var(--text-primary); }
  `],
})
export class MatchEndToastComponent {
  protected readonly toast = inject(ToastStore);
}
