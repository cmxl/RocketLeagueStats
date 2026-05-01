import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { PlayerStatsRow } from '../../core/models/player-stats';
import { KmhPipe } from '../../shared/pipes/kmh.pipe';

@Component({
  selector: 'rls-player-stats-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KmhPipe],
  template: `
    <div class="table-wrapper">
      <h3 class="table-title">Player Stats</h3>
      <table class="stats-table">
        <thead>
          <tr>
            <th>Player</th>
            <th>G</th>
            <th>A</th>
            <th>Sv</th>
            <th>Sh</th>
            <th>ESv</th>
            <th>D</th>
            <th>DT</th>
            <th>Best Speed</th>
            <th>Score</th>
          </tr>
        </thead>
        <tbody>
          @for (row of sortedRows(); track row.player.name) {
            <tr [class.row--blue]="row.player.team === 'blue'"
                [class.row--orange]="row.player.team === 'orange'"
                [class.row--mvp]="row.isMvp">
              <td class="player-cell">
                {{ row.player.name }}
                @if (row.isMvp) { <span class="mvp-badge">MVP</span> }
              </td>
              <td>{{ row.goals }}</td>
              <td>{{ row.assists }}</td>
              <td>{{ row.saves }}</td>
              <td>{{ row.shots }}</td>
              <td>{{ row.epicSaves }}</td>
              <td>{{ row.demosInflicted }}</td>
              <td>{{ row.demosTaken }}</td>
              <td>{{ row.fastestGoalSpeedUuPerSec | kmh }}</td>
              <td class="score-cell">{{ row.mvpScore }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .table-wrapper { padding: 1rem 1.5rem; overflow-x: auto; }
    .table-title { font-family: var(--font-header); font-size: var(--text-base); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.75rem; }
    .stats-table { width: 100%; border-collapse: collapse; font-family: var(--font-body); font-size: var(--text-sm); }
    .stats-table th { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; text-align: center; padding: 0.375rem 0.5rem; border-bottom: 1px solid var(--text-muted); }
    .stats-table th:first-child { text-align: left; }
    .stats-table td { padding: 0.375rem 0.5rem; text-align: center; color: var(--text-primary); border-bottom: 1px solid color-mix(in srgb, var(--text-muted) 40%, transparent); }
    .stats-table td:first-child { text-align: left; }
    .row--blue td:first-child { color: var(--team-blue); }
    .row--orange td:first-child { color: var(--team-orange); }
    .row--mvp { background: color-mix(in srgb, var(--accent-mvp) 8%, transparent); }
    .player-cell { display: flex; align-items: center; gap: 0.5rem; }
    .mvp-badge { font-size: var(--text-xs); background: var(--accent-mvp); color: var(--bg-base); padding: 0.1rem 0.3rem; border-radius: 2px; font-weight: 700; font-family: var(--font-header); }
    .score-cell { font-family: var(--font-display); font-size: var(--text-base); color: var(--accent-mvp); }
  `],
})
export class PlayerStatsTableComponent {
  readonly rows = input.required<PlayerStatsRow[]>();

  protected readonly sortedRows = computed(() =>
    [...this.rows()].sort((a, b) => {
      if (a.isMvp && !b.isMvp) return -1;
      if (!a.isMvp && b.isMvp) return 1;
      return b.mvpScore - a.mvpScore;
    }),
  );
}
