import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatchRecap } from '../../core/models/match';
import { KmhPipe } from '../../shared/pipes/kmh.pipe';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-speed-leaderboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KmhPipe, DurationPipe],
  template: `
    <div class="leaderboard">
      <h3 class="leaderboard__title">Fastest Goals</h3>
      <ol class="leaderboard__list">
        @for (goal of top3(); track goal.id; let i = $index) {
          <li class="leaderboard__item" [class.leaderboard__item--blue]="goal.scorer.team === 'blue'" [class.leaderboard__item--orange]="goal.scorer.team === 'orange'">
            <span class="leaderboard__rank">{{ i + 1 }}</span>
            <span class="leaderboard__name">{{ goal.scorer.name }}</span>
            <span class="leaderboard__speed">{{ goal.goalSpeedUuPerSec | kmh }}</span>
            <span class="leaderboard__clock">@ {{ goal.matchClockSeconds | duration }}</span>
          </li>
        } @empty {
          <li class="leaderboard__empty">No goals recorded.</li>
        }
      </ol>
    </div>
  `,
  styles: [`
    .leaderboard { padding: 1rem 1.5rem; }
    .leaderboard__title { font-family: var(--font-header); font-size: var(--text-base); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.75rem; }
    .leaderboard__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.5rem; }
    .leaderboard__item { display: flex; align-items: center; gap: 1rem; padding: 0.5rem 0.75rem; background: var(--bg-overlay); border-radius: 4px; border-left: 3px solid var(--text-muted); }
    .leaderboard__item--blue { border-left-color: var(--team-blue); }
    .leaderboard__item--orange { border-left-color: var(--team-orange); }
    .leaderboard__rank { font-family: var(--font-display); font-size: var(--text-xl); color: var(--text-muted); width: 1.5rem; text-align: center; line-height: 1; }
    .leaderboard__name { flex: 1; font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-primary); font-weight: 600; }
    .leaderboard__speed { font-family: var(--font-display); font-size: var(--text-lg); color: var(--accent-cyan); }
    .leaderboard__clock { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); }
    .leaderboard__empty { color: var(--text-muted); font-size: var(--text-sm); text-align: center; padding: 1rem; }
  `],
})
export class SpeedLeaderboardComponent {
  readonly recap = input.required<MatchRecap>();

  protected readonly top3 = computed(() =>
    [...this.recap().goals]
      .sort((a, b) => b.goalSpeedUuPerSec - a.goalSpeedUuPerSec)
      .slice(0, 3),
  );
}
