import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { httpResource } from '@angular/common/http';
import { MatchRecap } from '../../core/models/match';
import { HeroSectionComponent } from './hero-section.component';
import { PlayerStatsTableComponent } from './player-stats-table.component';
import { SpeedLeaderboardComponent } from './speed-leaderboard.component';
import { GoalTimelineChartComponent } from './goal-timeline.chart.component';
import { TimeBetweenGoalsChartComponent } from './time-between-goals.chart.component';
import { GameFlowChartComponent } from './game-flow.chart.component';
import { EventTimelineComponent } from './event-timeline.component';

@Component({
  selector: 'rls-recap-view',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    HeroSectionComponent,
    PlayerStatsTableComponent,
    SpeedLeaderboardComponent,
    GoalTimelineChartComponent,
    TimeBetweenGoalsChartComponent,
    GameFlowChartComponent,
    EventTimelineComponent,
  ],
  template: `
    @if (recap.isLoading()) {
      <p class="recap-state">Loading recap…</p>
    } @else if (recap.error()) {
      <p class="recap-state recap-state--error">
        Match not found. <a routerLink="/history">Return to history</a>
      </p>
    } @else if (recap.value(); as r) {
      <rls-hero-section [recap]="r" />
      <div class="recap-grid">
        @defer (on viewport) {
          <rls-goal-timeline-chart [recap]="r" />
          <rls-time-between-goals-chart [recap]="r" />
          <rls-game-flow-chart [recap]="r" />
        } @placeholder {
          <div class="charts-placeholder">Charts loading…</div>
        }
        <rls-speed-leaderboard [recap]="r" />
        <rls-player-stats-table [rows]="r.playerStats" />
        @defer (on viewport) {
          <rls-event-timeline [recap]="r" />
        } @placeholder {
          <div class="charts-placeholder">Event timeline loading…</div>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: block; }
    .recap-state { padding: 2rem 1.5rem; color: var(--text-secondary); font-family: var(--font-header); text-align: center; }
    .recap-state--error { color: var(--accent-danger); }
    .recap-state a { color: var(--accent-cyan); }
    .recap-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; padding: 1.5rem; }
    .charts-placeholder { grid-column: 1 / -1; padding: 2rem; text-align: center; color: var(--text-muted); font-family: var(--font-header); }
  `],
})
export class RecapViewComponent {
  readonly matchId = input.required<string>();

  protected readonly recap = httpResource<MatchRecap>(() => ({
    url: `/api/matches/${encodeURIComponent(this.matchId())}`,
  }));
}
