import { Component, ChangeDetectionStrategy, inject, computed } from '@angular/core';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { ActionFeedItemComponent } from './action-feed-item.component';
import { Goal } from '../../core/models/goal';
import { Statfeed } from '../../core/models/statfeed';

interface FeedEntry {
  id: string;
  kind: 'goal' | 'statfeed';
  event: Goal | Statfeed;
  ts: string;
}

@Component({
  selector: 'rls-action-feed',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ActionFeedItemComponent],
  template: `
    <div class="action-feed">
      <h3 class="action-feed__title">Recent Actions</h3>
      <ul class="action-feed__list">
        @for (entry of entries(); track entry.id) {
          <li class="action-feed__item">
            <rls-action-feed-item [event]="entry.event" [kind]="entry.kind" />
          </li>
        } @empty {
          <li class="action-feed__empty">No actions yet</li>
        }
      </ul>
    </div>
  `,
  styles: [`
    .action-feed { display: flex; flex-direction: column; gap: 0; min-height: 0; height: 100%; }
    .action-feed__title { font-family: var(--font-header); font-size: var(--text-sm); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.5rem; padding: 0 0.75rem; flex-shrink: 0; }
    .action-feed__list { list-style: none; margin: 0; padding: 0 0.25rem; display: flex; flex-direction: column; gap: 0.25rem; overflow-y: auto; min-height: 0; flex: 1; }
    .action-feed__item { animation: rls-fade-in 200ms ease; }
    .action-feed__empty { padding: 0.75rem; color: var(--text-muted); font-size: var(--text-sm); text-align: center; }
  `],
})
export class ActionFeedComponent {
  private readonly live = inject(LiveMatchStore);

  // Live awareness focuses on recent activity; the underlying store keeps the full match history
  // (consumed by the recap timeline). Cap render to the newest 30 here — the list scrolls if the
  // user wants to look further back.
  private static readonly MAX_VISIBLE_ENTRIES = 30;

  protected readonly entries = computed<FeedEntry[]>(() => {
    const goals = this.live.goals().map(g => ({
      id: g.id,
      kind: 'goal' as const,
      event: g,
      ts: g.timestamp,
    }));
    const sfs = this.live.statfeeds().map((s, i) => ({
      id: `sf-${s.timestamp}-${i}`,
      kind: 'statfeed' as const,
      event: s,
      ts: s.timestamp,
    }));
    return [...goals, ...sfs]
      .sort((a, b) => b.ts.localeCompare(a.ts))
      .slice(0, ActionFeedComponent.MAX_VISIBLE_ENTRIES);
  });
}
