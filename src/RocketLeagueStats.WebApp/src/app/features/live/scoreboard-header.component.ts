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
      @if (showPossession()) {
        <div class="possession" [attr.aria-label]="'Ball possession ' + bluePossessionPct() + ' percent blue / ' + orangePossessionPct() + ' percent orange'">
          <div class="possession__bar">
            <div class="possession__fill possession__fill--blue" [style.width.%]="bluePossessionPct()"></div>
            <div class="possession__fill possession__fill--orange" [style.width.%]="orangePossessionPct()"></div>
          </div>
          <div class="possession__labels">
            <span class="possession__label possession__label--blue">{{ bluePossessionPct() }}%</span>
            <span class="possession__caption">POSSESSION</span>
            <span class="possession__label possession__label--orange">{{ orangePossessionPct() }}%</span>
          </div>
        </div>
      }
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
    .possession { width: 100%; max-width: 480px; display: flex; flex-direction: column; gap: 0.25rem; }
    .possession__bar { display: flex; height: 6px; border-radius: 3px; overflow: hidden; background: var(--bg-overlay); }
    .possession__fill { height: 100%; transition: width 250ms ease-out; }
    .possession__fill--blue { background: var(--team-blue); }
    .possession__fill--orange { background: var(--team-orange); }
    .possession__labels { display: flex; justify-content: space-between; align-items: center; font-family: var(--font-header); font-size: var(--text-xs); }
    .possession__label--blue { color: var(--team-blue); font-family: var(--font-display); font-size: var(--text-sm); }
    .possession__label--orange { color: var(--team-orange); font-family: var(--font-display); font-size: var(--text-sm); }
    .possession__caption { color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.08em; }
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

  // Possession is the share of total ball touches per team. Touches come from the wire's
  // MatchStateSnapshot; until the first snapshot lands every player has 0 touches and the bar
  // is hidden via `showPossession`. We round to whole percentages so blue% + orange% = 100
  // (rounding the larger up; otherwise low-touch warm-up minutes flicker between 49/51 etc.).
  private readonly blueTouches = computed(() =>
    this.live.playerStats().filter(p => p.player.team === 'blue').reduce((acc, p) => acc + p.touches, 0));

  private readonly orangeTouches = computed(() =>
    this.live.playerStats().filter(p => p.player.team === 'orange').reduce((acc, p) => acc + p.touches, 0));

  protected readonly showPossession = computed(() => this.blueTouches() + this.orangeTouches() > 0);

  protected readonly bluePossessionPct = computed(() => {
    const blue = this.blueTouches();
    const total = blue + this.orangeTouches();
    return total === 0 ? 0 : Math.round((blue * 100) / total);
  });

  protected readonly orangePossessionPct = computed(() => 100 - this.bluePossessionPct());
}
