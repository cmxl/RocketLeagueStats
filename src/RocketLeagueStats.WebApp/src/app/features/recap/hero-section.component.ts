import { Component, ChangeDetectionStrategy, input, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatchRecap } from '../../core/models/match';
import { MatchTypeBadgeComponent } from '../../shared/components/match-type-badge.component';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { KmhPipe } from '../../shared/pipes/kmh.pipe';
import { SettingsStore } from '../../core/state/settings.store';

@Component({
  selector: 'rls-hero-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatchTypeBadgeComponent, DurationPipe, KmhPipe, DatePipe],
  template: `
    <!-- Per-match team colors land on --card-blue / --card-orange via inline style; the
         win/loss left-edge stripe rides on a class. Same visual language as the history card
         so the user sees consistent W/L marking from list -> recap. -->
    <div class="hero"
         [class.hero--win]="outcome() === 'win'"
         [class.hero--loss]="outcome() === 'loss'"
         [style.--card-blue]="blueColor()"
         [style.--card-orange]="orangeColor()">
      <div class="hero__meta">
        <rls-match-type-badge [type]="recap().summary.type" />
        @if (recap().summary.arenaName; as arena) {
          <span class="hero__arena">{{ arena }}</span>
        }
        <time class="hero__date">{{ recap().summary.endedAt | date:'medium' }}</time>
        <span class="hero__duration">{{ recap().summary.durationSeconds | duration }}</span>
      </div>
      <div class="hero__score">
        <div class="hero__team hero__team--blue">
          <span class="hero__team-label">{{ blueLabel() }}</span>
          <span class="hero__team-score">{{ recap().summary.blueScore }}</span>
        </div>
        <span class="hero__dash">—</span>
        <div class="hero__team hero__team--orange">
          <span class="hero__team-score">{{ recap().summary.orangeScore }}</span>
          <span class="hero__team-label">{{ orangeLabel() }}</span>
        </div>
      </div>
      @if (recap().summary.mvp; as mvp) {
        <div class="hero__mvp">
          <span class="hero__mvp-label">MVP</span>
          <span class="hero__mvp-name">{{ mvp.name }}</span>
        </div>
      }
      <div class="hero__stats">
        <div class="hero__stat">
          <span class="hero__stat-value">{{ recap().summary.totalGoals }}</span>
          <span class="hero__stat-label">Goals</span>
        </div>
        @if (recap().summary.fastestGoal; as fastest) {
          <div class="hero__stat">
            <span class="hero__stat-value">{{ fastest.goalSpeedUuPerSec | kmh }}</span>
            <span class="hero__stat-label">Fastest Goal</span>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .hero { padding: 2rem 1.5rem 2rem 2rem; text-align: center; background: var(--bg-elevated); border-bottom: 1px solid var(--text-muted); position: relative; }
    /* 4px outcome stripe — matches match-card.component.ts. ::before keeps the stripe out of
       the content flow so toggling the class doesn't shift the score column. */
    .hero::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      bottom: 0;
      width: 4px;
      background: transparent;
    }
    .hero--win::before  { background: var(--accent-success); }
    .hero--loss::before { background: var(--accent-danger); }
    .hero__meta { display: flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem; font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-secondary); }
    .hero__arena { text-transform: uppercase; letter-spacing: 0.06em; color: var(--text-muted); }
    .hero__score { display: flex; align-items: center; justify-content: center; gap: 2rem; font-family: var(--font-display); font-size: var(--text-display-md); line-height: 1; }
    .hero__team { display: flex; align-items: center; gap: 0.75rem; }
    .hero__team--blue  .hero__team-score { color: var(--card-blue,  var(--team-blue));  }
    .hero__team--orange .hero__team-score { color: var(--card-orange, var(--team-orange)); }
    /* Team-name labels share the per-match color so the user reads name + score as one unit. */
    .hero__team--blue  .hero__team-label { color: var(--card-blue,  var(--team-blue));  }
    .hero__team--orange .hero__team-label { color: var(--card-orange, var(--team-orange)); }
    .hero__team-label { font-family: var(--font-header); font-size: var(--text-sm); text-transform: uppercase; letter-spacing: 0.08em; }
    .hero__dash { color: var(--text-muted); font-size: var(--text-2xl); }
    .hero__mvp { margin-top: 1rem; display: flex; align-items: center; justify-content: center; gap: 0.5rem; }
    .hero__mvp-label { font-family: var(--font-header); font-size: var(--text-xs); color: var(--accent-mvp); text-transform: uppercase; letter-spacing: 0.08em; background: color-mix(in srgb, var(--accent-mvp) 15%, transparent); padding: 0.1rem 0.4rem; border-radius: 2px; }
    .hero__mvp-name { font-family: var(--font-header); font-size: var(--text-lg); color: var(--text-primary); font-weight: 700; }
    .hero__stats { display: flex; justify-content: center; gap: 2rem; margin-top: 1rem; }
    .hero__stat { display: flex; flex-direction: column; align-items: center; }
    .hero__stat-value { font-family: var(--font-display); font-size: var(--text-2xl); color: var(--text-primary); line-height: 1; }
    .hero__stat-label { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; }
  `],
})
export class HeroSectionComponent {
  readonly recap = input.required<MatchRecap>();

  private readonly settings = inject(SettingsStore);

  protected readonly blueColor = computed(() => {
    const team = this.recap().summary.blueTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly orangeColor = computed(() => {
    const team = this.recap().summary.orangeTeam;
    return team ? `#${team.colorPrimary}` : 'unset';
  });

  protected readonly blueLabel = computed(() =>
    (this.recap().summary.blueTeam?.name ?? 'BLUE').toUpperCase());

  protected readonly orangeLabel = computed(() =>
    (this.recap().summary.orangeTeam?.name ?? 'ORANGE').toUpperCase());

  // Same outcome rule as MatchCardComponent — keep these in sync if either changes. Configured
  // player not in match (or no configured player at all) = 'none', so the stripe stays clear
  // for spectated matches the user replays from elsewhere.
  protected readonly outcome = computed<'win' | 'loss' | 'none'>(() => {
    const summary = this.recap().summary;
    const winner = summary.winnerTeamNum;
    const playerName = this.settings.current().playerName?.trim();
    if (winner === null || winner === undefined || !playerName) {
      return 'none';
    }

    const me = summary.allPlayers.find(p => p.name.toLowerCase() === playerName.toLowerCase());
    if (!me) {
      return 'none';
    }

    const myTeamNum = me.team === 'blue' ? 0 : me.team === 'orange' ? 1 : -1;
    if (myTeamNum === -1) {
      return 'none';
    }

    return myTeamNum === winner ? 'win' : 'loss';
  });
}
