import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatchType } from '../../core/models';

@Component({
  selector: 'rls-match-type-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" [class]="'badge--' + type()">{{ label() }}</span>
  `,
  styles: [`
    .badge {
      display: inline-block;
      padding: 0.125rem 0.5rem;
      border-radius: 2px;
      font-family: var(--font-header);
      font-size: var(--text-xs);
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      background: var(--bg-overlay);
      color: var(--text-secondary);
      border: 1px solid var(--text-muted);
    }
    .badge--ranked1v1, .badge--ranked2v2, .badge--ranked3v3, .badge--online {
      color: var(--accent-cyan);
      border-color: var(--accent-cyan);
    }
    .badge--tournament {
      color: var(--accent-mvp);
      border-color: var(--accent-mvp);
    }
    .badge--casual {
      color: var(--accent-success);
      border-color: var(--accent-success);
    }
  `],
})
export class MatchTypeBadgeComponent {
  readonly type = input.required<MatchType>();

  protected readonly label = computed(() => {
    const map: Record<MatchType, string> = {
      unknown: 'Unknown',
      ranked1v1: '1v1',
      ranked2v2: '2v2',
      ranked3v3: '3v3',
      casual: 'Casual',
      tournament: 'Tournament',
      private: 'Private',
      freePlay: 'Free Play',
      training: 'Training',
      online: 'Online',
      offline: 'Offline',
    };
    return map[this.type()] ?? this.type();
  });
}
