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
            <!-- <abbr title=...> attaches a tooltip on hover and is announced by screen readers,
                 unlike a bare title= on <th>. The dotted underline in user-agent default
                 styling is suppressed via .stats-table abbr below to keep the header clean. -->
            <th><abbr title="Goals">G</abbr></th>
            <th><abbr title="Assists">A</abbr></th>
            <th><abbr title="Saves">Sv</abbr></th>
            <th><abbr title="Shots on goal">Sh</abbr></th>
            <th><abbr title="Epic Saves">ESv</abbr></th>
            <th><abbr title="Demolitions inflicted">D</abbr></th>
            <th><abbr title="Demolitions taken">DT</abbr></th>
            <th><abbr title="Fastest goal speed">Best Speed</abbr></th>
            <th><abbr title="In-game score (from Rocket League's scoreboard)">Score</abbr></th>
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
              <td class="score-cell">{{ row.score }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    /* Span the full recap-grid row — same pattern as the event timeline — so the 10-column
       table never has to compete for horizontal space with adjacent grid items. */
    :host { grid-column: 1 / -1; }
    .table-wrapper { padding: 1rem 1.5rem; overflow-x: auto; }
    .table-title { font-family: var(--font-header); font-size: var(--text-base); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.75rem; }
    .stats-table { width: 100%; border-collapse: collapse; font-family: var(--font-body); font-size: var(--text-sm); }
    .stats-table th { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; text-align: center; padding: 0.375rem 0.5rem; border-bottom: 1px solid var(--text-muted); }
    .stats-table th:first-child { text-align: left; }
    /* Suppress the user-agent dotted underline on <abbr title>. We get the help-cursor from
       cursor: help instead, which signals discoverability without competing with the table's
       muted-uppercase header look. */
    .stats-table abbr {
      text-decoration: none;
      cursor: help;
      border-bottom: 1px dotted color-mix(in srgb, var(--text-muted) 60%, transparent);
    }
    .stats-table td { padding: 0.375rem 0.5rem; text-align: center; color: var(--text-primary); border-bottom: 1px solid color-mix(in srgb, var(--text-muted) 40%, transparent); vertical-align: middle; line-height: 1.4; }
    .stats-table td:first-child { text-align: left; }
    .row--blue td:first-child { color: var(--team-blue); }
    .row--orange td:first-child { color: var(--team-orange); }
    .row--mvp { background: color-mix(in srgb, var(--accent-mvp) 8%, transparent); }
    /* Keep the player TD as a regular table-cell so it aligns with the numeric columns;
       the MVP badge rides as inline-block so it doesn't push the row taller. */
    .player-cell { white-space: nowrap; }
    .mvp-badge { display: inline-block; margin-left: 0.4rem; vertical-align: middle; font-size: var(--text-xs); background: var(--accent-mvp); color: var(--bg-base); padding: 0.1rem 0.3rem; border-radius: 2px; font-weight: 700; font-family: var(--font-header); line-height: 1; }
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
