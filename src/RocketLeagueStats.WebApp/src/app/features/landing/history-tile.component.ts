import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { httpResource } from '@angular/common/http';
import { MatchSummary } from '../../core/models';
import { PanelComponent } from '../../shared/components/panel.component';

@Component({
  selector: 'rls-history-tile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PanelComponent],
  template: `
    <rls-panel>
      <div class="tile-content">
        <h2 class="tile-title">Match History</h2>
        @if (matches.isLoading()) {
          <p class="tile-stat">Loading…</p>
        } @else if (matches.value(); as list) {
          <p class="tile-stat">
            <span class="tile-stat__number">{{ list.length }}</span>
            <span class="tile-stat__label"> matches recorded</span>
          </p>
        } @else {
          <p class="tile-empty">No matches recorded yet.</p>
        }
        <a class="tile-cta" routerLink="/history">View History →</a>
      </div>
    </rls-panel>
  `,
  styles: [`
    .tile-content { padding: 1rem; }
    .tile-title { margin: 0 0 0.75rem; font-family: var(--font-header); font-size: var(--text-xl); color: var(--text-primary); }
    .tile-stat { margin: 0.5rem 0; }
    .tile-stat__number { font-family: var(--font-display); font-size: var(--text-3xl); color: var(--accent-cyan); }
    .tile-stat__label { color: var(--text-secondary); font-size: var(--text-sm); }
    .tile-empty { color: var(--text-secondary); margin: 0.5rem 0; font-size: var(--text-sm); }
    .tile-cta { display: inline-block; margin-top: 0.75rem; color: var(--accent-cyan); text-decoration: none; font-family: var(--font-header); font-weight: 600; font-size: var(--text-sm); text-transform: uppercase; letter-spacing: 0.05em; }
    .tile-cta:hover { text-decoration: underline; }
  `],
})
export class HistoryTileComponent {
  protected readonly matches = httpResource<MatchSummary[]>(() => ({ url: '/api/matches' }));
}
