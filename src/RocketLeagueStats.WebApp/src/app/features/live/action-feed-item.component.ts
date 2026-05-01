import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { Goal } from '../../core/models/goal';
import { Statfeed } from '../../core/models/statfeed';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-action-feed-item',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DurationPipe],
  template: `
    @switch (kind()) {
      @case ('goal') {
        @let g = asGoal();
        @if (g) {
          <div class="feed-item feed-item--goal" [class.feed-item--blue]="g.scorer.team === 'blue'" [class.feed-item--orange]="g.scorer.team === 'orange'">
            <span class="feed-item__icon">&#9918;</span>
            <span class="feed-item__text">
              <strong>{{ g.scorer.name }}</strong> scored
              @if (g.assister) { <span class="feed-item__assist"> (assist: {{ g.assister.name }})</span> }
            </span>
            <span class="feed-item__time">{{ g.matchClockSeconds | duration }}</span>
          </div>
        }
      }
      @case ('statfeed') {
        @let sf = asStatfeed();
        @if (sf) {
          <div class="feed-item feed-item--statfeed" [class.feed-item--blue]="sf.mainTarget.team === 'blue'" [class.feed-item--orange]="sf.mainTarget.team === 'orange'">
            <span class="feed-item__icon">&#9889;</span>
            <span class="feed-item__text">
              <strong>{{ sf.mainTarget.name }}</strong> — {{ sf.type }}
              @if (sf.secondaryTarget) { <span class="feed-item__secondary"> on {{ sf.secondaryTarget.name }}</span> }
            </span>
            <span class="feed-item__time">{{ sf.matchClockSeconds | duration }}</span>
          </div>
        }
      }
    }
  `,
  styles: [`
    .feed-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      border-left: 3px solid var(--text-muted);
      border-radius: 2px;
      font-size: var(--text-sm);
      background: var(--bg-overlay);
    }
    .feed-item--blue { border-left-color: var(--team-blue); }
    .feed-item--orange { border-left-color: var(--team-orange); }
    .feed-item--goal { background: color-mix(in srgb, var(--bg-overlay) 90%, var(--accent-mvp) 10%); }
    .feed-item__icon { font-size: var(--text-base); }
    .feed-item__text { flex: 1; color: var(--text-primary); }
    .feed-item__assist, .feed-item__secondary { color: var(--text-secondary); }
    .feed-item__time { color: var(--text-muted); font-family: var(--font-display); font-size: var(--text-xs); white-space: nowrap; }
  `],
})
export class ActionFeedItemComponent {
  readonly event = input.required<Goal | Statfeed>();
  readonly kind = input.required<'goal' | 'statfeed'>();

  protected asGoal(): Goal | null {
    return this.kind() === 'goal' ? (this.event() as Goal) : null;
  }

  protected asStatfeed(): Statfeed | null {
    return this.kind() === 'statfeed' ? (this.event() as Statfeed) : null;
  }
}
