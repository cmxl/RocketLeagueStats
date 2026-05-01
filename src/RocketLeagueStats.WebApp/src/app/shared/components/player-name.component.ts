import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { PlayerRef } from '../../core/models';

@Component({
  selector: 'rls-player-name',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="player-name" [class.player-name--own]="isOwn()">{{ player().name }}</span>
  `,
  styles: [`
    .player-name {
      font-family: var(--font-header);
      font-weight: 600;
      color: var(--text-primary);
    }
    .player-name--own {
      color: var(--accent-mvp);
    }
  `],
})
export class PlayerNameComponent {
  readonly player = input.required<PlayerRef>();
  readonly isOwn = input<boolean>(false);
}
