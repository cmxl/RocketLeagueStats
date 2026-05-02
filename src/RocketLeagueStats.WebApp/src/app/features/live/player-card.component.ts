import { Component, ChangeDetectionStrategy, input, inject, computed } from '@angular/core';
import { PlayerStatsRow } from '../../core/models/player-stats';
import { PanelComponent } from '../../shared/components/panel.component';
import { SettingsStore } from '../../core/state/settings.store';

@Component({
  selector: 'rls-player-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PanelComponent],
  template: `
    <rls-panel [team]="team()" [glow]="isOwn()">
      <div class="player-card">
        <div class="player-card__header">
          <h3 class="player-card__name" [class.player-card__name--you]="isOwn()">
            {{ player().player.name }}
            @if (isOwn()) { <span class="player-card__you">YOU</span> }
          </h3>
          @if (platform()) {
            <span class="player-card__platform">{{ platform() }}</span>
          }
        </div>
        <dl class="stats">
          <div class="stat"><dt>G</dt><dd>{{ player().goals }}</dd></div>
          <div class="stat"><dt>A</dt><dd>{{ player().assists }}</dd></div>
          <div class="stat"><dt>Sv</dt><dd>{{ player().saves }}</dd></div>
          <div class="stat"><dt>Sh</dt><dd>{{ player().shots }}</dd></div>
          <div class="stat"><dt>D</dt><dd>{{ player().demosInflicted }}</dd></div>
        </dl>
      </div>
    </rls-panel>
  `,
  styles: [`
    .player-card { padding: 0.75rem; }
    .player-card__header { display: flex; align-items: baseline; gap: 0.5rem; margin: 0 0 0.5rem; flex-wrap: wrap; }
    .player-card__name { font-family: var(--font-header); font-size: var(--text-base); font-weight: 700; margin: 0; color: var(--text-primary); display: flex; align-items: center; gap: 0.5rem; }
    .player-card__name--you { color: var(--accent-mvp); }
    .player-card__you { font-size: var(--text-xs); background: var(--accent-mvp); color: var(--bg-base); padding: 0.1rem 0.35rem; border-radius: 2px; font-weight: 700; }
    .player-card__platform { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; padding: 0.1rem 0.35rem; border: 1px solid color-mix(in srgb, var(--text-muted) 60%, transparent); border-radius: 2px; }
    .stats { display: flex; gap: 0.75rem; margin: 0; padding: 0; }
    .stat { display: flex; flex-direction: column; align-items: center; }
    .stat dt { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; }
    .stat dd { font-family: var(--font-display); font-size: var(--text-lg); color: var(--text-primary); margin: 0; }
  `],
})
export class PlayerCardComponent {
  readonly player = input.required<PlayerStatsRow>();
  private readonly settings = inject(SettingsStore);

  protected readonly team = computed(() => this.player().player.team === 'blue' ? 'blue' as const : 'orange' as const);
  protected readonly isOwn = computed(() => {
    const myName = this.settings.current().playerName;
    return !!myName && myName === this.player().player.name;
  });
  protected readonly platform = computed(() => this.player().player.platform);
}
