import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { PanelComponent } from '../../shared/components/panel.component';

@Component({
  selector: 'rls-live-tile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, PanelComponent],
  template: `
    @let live = liveMatch.hasLiveMatch();
    <rls-panel [glow]="live">
      <div class="tile-content">
        <h2 class="tile-title">Live Match</h2>
        @if (live) {
          <p class="big-score">
            <span class="blue">{{ liveMatch.blueScore() }}</span>
            <span class="dash"> — </span>
            <span class="orange">{{ liveMatch.orangeScore() }}</span>
          </p>
          <a class="tile-cta" routerLink="/live">Open Live View →</a>
        } @else {
          <p class="tile-empty">No live match. Start one in Rocket League.</p>
          <a class="tile-cta tile-cta--muted" routerLink="/live">Watch Live →</a>
        }
      </div>
    </rls-panel>
  `,
  styles: [`
    .tile-content { padding: 1rem; }
    .tile-title { margin: 0 0 0.75rem; font-family: var(--font-header); font-size: var(--text-xl); color: var(--text-primary); }
    .big-score { font-family: var(--font-display); font-size: var(--text-3xl); margin: 0.5rem 0; }
    .blue { color: var(--team-blue); }
    .orange { color: var(--team-orange); }
    .dash { color: var(--text-muted); }
    .tile-empty { color: var(--text-secondary); margin: 0.5rem 0; font-size: var(--text-sm); }
    .tile-cta { display: inline-block; margin-top: 0.75rem; color: var(--accent-cyan); text-decoration: none; font-family: var(--font-header); font-weight: 600; font-size: var(--text-sm); text-transform: uppercase; letter-spacing: 0.05em; }
    .tile-cta--muted { color: var(--text-secondary); }
    .tile-cta:hover { text-decoration: underline; }
  `],
})
export class LiveTileComponent {
  protected readonly liveMatch = inject(LiveMatchStore);
}
