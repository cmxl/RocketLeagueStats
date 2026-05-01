import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { ScoreboardHeaderComponent } from './scoreboard-header.component';
import { ActionFeedComponent } from './action-feed.component';
import { PlayerCardComponent } from './player-card.component';
import { TimeSinceGoalComponent } from './time-since-goal.component';
import { GoalOverlayComponent } from './goal-overlay.component';
import { LiveMatchStore } from '../../core/state/live-match.store';

@Component({
  selector: 'rls-live-view',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ScoreboardHeaderComponent,
    ActionFeedComponent,
    PlayerCardComponent,
    TimeSinceGoalComponent,
    GoalOverlayComponent,
  ],
  template: `
    <rls-scoreboard-header />
    <div class="live-grid">
      <div class="live-grid__players live-grid__players--blue">
        @for (player of bluePlayers(); track player.player.name) {
          <rls-player-card [player]="player" />
        }
      </div>
      <rls-action-feed class="live-grid__feed" />
      <div class="live-grid__players live-grid__players--orange">
        @for (player of orangePlayers(); track player.player.name) {
          <rls-player-card [player]="player" />
        }
      </div>
    </div>
    <rls-time-since-goal />
    <rls-goal-overlay />
  `,
  styles: [`
    :host { display: flex; flex-direction: column; height: 100%; }
    .live-grid {
      display: grid;
      grid-template-columns: 1fr 2fr 1fr;
      gap: 1rem;
      flex: 1;
      padding: 1rem;
      min-height: 0;
    }
    .live-grid__players { display: flex; flex-direction: column; gap: 0.75rem; }
    .live-grid__feed { min-width: 0; }
    @media (max-width: 768px) {
      .live-grid { grid-template-columns: 1fr; }
    }
  `],
})
export class LiveViewComponent {
  private readonly live = inject(LiveMatchStore);

  protected readonly bluePlayers = computed(() =>
    this.live.playerStats().filter(p => p.player.team === 'blue'),
  );

  protected readonly orangePlayers = computed(() =>
    this.live.playerStats().filter(p => p.player.team === 'orange'),
  );
}
