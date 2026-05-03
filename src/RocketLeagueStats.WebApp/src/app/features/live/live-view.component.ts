import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { ScoreboardHeaderComponent } from './scoreboard-header.component';
import { ActionFeedComponent } from './action-feed.component';
import { PlayerCardComponent } from './player-card.component';
import { TimeSinceGoalComponent } from './time-since-goal.component';
import { GoalOverlayComponent } from './goal-overlay.component';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { PlayerRef } from '../../core/models/player';
import { PlayerStatsRow } from '../../core/models/player-stats';

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

  // Render the full team roster from the match header (populated by the first MatchStateSnapshot
  // tick), matching each entry to its tally row from `playerStats`. Players who haven't yet
  // generated a goal/statfeed get an all-zero placeholder row so their card still appears with
  // their name and platform — instead of waiting for them to do something visible. Falls back to
  // the lazy `playerStats` list during the brief MatchInitialized → first-snapshot window.
  protected readonly bluePlayers = computed(() => this.combineRoster('blue'));
  protected readonly orangePlayers = computed(() => this.combineRoster('orange'));

  private combineRoster(team: 'blue' | 'orange'): PlayerStatsRow[] {
    const stats = this.live.playerStats();
    const roster = team === 'blue'
      ? this.live.currentMatch()?.bluePlayers
      : this.live.currentMatch()?.orangePlayers;

    if (!roster || roster.length === 0) {
      return [...stats.filter(p => p.player.team === team)].sort(byScoreDesc);
    }

    // Mirror the in-game scoreboard: highest-scoring player at the top of each team column.
    // Score comes from the wire's MatchStateSnapshot via SnapshotPlayer.Score (same field that
    // backs the Score column in the recap player-stats table). During the brief window between
    // MatchInitialized and the first snapshot tick every entry has score=0, so the comparator
    // is effectively a no-op then and roster order wins — which keeps the UI from flickering
    // between renders while waiting for real values.
    return roster
      .map(p => stats.find(s => s.player.shortcut === p.shortcut) ?? emptyRow(p))
      .sort(byScoreDesc);
  }
}

function emptyRow(player: PlayerRef): PlayerStatsRow {
  return {
    player,
    goals: 0,
    assists: 0,
    saves: 0,
    epicSaves: 0,
    shots: 0,
    demosInflicted: 0,
    demosTaken: 0,
    crossbarHits: 0,
    fastestGoalSpeedUuPerSec: 0,
    mvpScore: 0,
    isMvp: false,
    score: 0,
    touches: 0,
  };
}

// Stable sort by score DESC. Array.prototype.sort isn't guaranteed stable across all engines
// per the spec, but is stable in V8/SpiderMonkey/JavaScriptCore as of ES2019 — which covers
// every browser this app ships against. Equal scores keep the roster's emission order, so
// the column doesn't visually scramble during the all-zero pre-snapshot window.
function byScoreDesc(a: PlayerStatsRow, b: PlayerStatsRow): number {
  return b.score - a.score;
}
