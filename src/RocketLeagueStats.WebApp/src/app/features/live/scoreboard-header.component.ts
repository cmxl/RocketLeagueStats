import { Component, ChangeDetectionStrategy, computed, inject } from '@angular/core';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { MatchTypeBadgeComponent } from '../../shared/components/match-type-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-scoreboard-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatchTypeBadgeComponent, DurationPipe],
  template: `
    <div class="scoreboard"
         [style.--team-blue]="blueColor()"
         [style.--team-orange]="orangeColor()">
      <div class="scoreboard__meta">
        @if (live.currentMatch(); as match) {
          <rls-match-type-badge [type]="match.type" />
          <span class="scoreboard__arena">{{ match.arenaName }}</span>
        }
        @if (live.clockSeconds() != null) {
          <span class="scoreboard__clock">{{ live.clockSeconds() | duration }}</span>
        }
      </div>
      <div class="scoreboard__scores">
        <div class="scoreboard__team scoreboard__team--blue">
          <span class="scoreboard__team-label">{{ blueLabel() }}</span>
          <span class="scoreboard__score">{{ live.blueScore() }}</span>
        </div>
        <span class="scoreboard__divider">—</span>
        <div class="scoreboard__team scoreboard__team--orange">
          <span class="scoreboard__score">{{ live.orangeScore() }}</span>
          <span class="scoreboard__team-label">{{ orangeLabel() }}</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .scoreboard {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 1.25rem 1.5rem;
      background: var(--bg-elevated);
      border-bottom: 1px solid var(--text-muted);
    }
    .scoreboard__meta {
      display: flex;
      align-items: center;
      gap: 1rem;
      font-family: var(--font-header);
      font-size: var(--text-sm);
      color: var(--text-secondary);
    }
    .scoreboard__arena { text-transform: uppercase; letter-spacing: 0.04em; }
    .scoreboard__clock { font-family: var(--font-display); font-size: var(--text-lg); color: var(--text-primary); }
    .scoreboard__scores {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      font-family: var(--font-display);
      font-size: var(--text-display-md);
      line-height: 1;
    }
    .scoreboard__team { display: flex; align-items: center; gap: 0.75rem; }
    .scoreboard__team--blue .scoreboard__score { color: var(--team-blue); }
    .scoreboard__team--orange .scoreboard__score { color: var(--team-orange); }
    .scoreboard__team-label { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.08em; }
    .scoreboard__divider { color: var(--text-muted); font-size: var(--text-2xl); }
  `],
})
export class ScoreboardHeaderComponent {
  protected readonly live = inject(LiveMatchStore);

  // Snapshot-driven team metadata. Falls back to the existing CSS variables (`--team-blue` /
  // `--team-orange`) until the first MatchStateSnapshot of a match arrives — `'unset'` lets
  // the inherited variable show through instead of overriding it with an empty string.
  protected readonly blueColor = computed(() => {
    const team = this.live.currentMatch()?.blueTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly orangeColor = computed(() => {
    const team = this.live.currentMatch()?.orangeTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly blueLabel = computed(() =>
    (this.live.currentMatch()?.blueTeam?.name ?? 'BLUE').toUpperCase());

  protected readonly orangeLabel = computed(() =>
    (this.live.currentMatch()?.orangeTeam?.name ?? 'ORANGE').toUpperCase());
}
