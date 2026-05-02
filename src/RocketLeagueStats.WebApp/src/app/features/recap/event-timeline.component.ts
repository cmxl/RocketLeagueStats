import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatchRecap } from '../../core/models/match';
import { Goal } from '../../core/models/goal';
import { Statfeed } from '../../core/models/statfeed';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { KmhPipe } from '../../shared/pipes/kmh.pipe';
import { StatfeedIconPipe } from '../../shared/pipes/statfeed-icon.pipe';

interface TimelineEntry {
  id: string;
  kind: 'goal' | 'statfeed';
  matchClockSeconds: number;
  goal?: Goal;
  statfeed?: Statfeed;
}

@Component({
  selector: 'rls-event-timeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DurationPipe, KmhPipe, StatfeedIconPipe],
  // Chronological narrative — oldest first — so the recap reads as a story of the match.
  // We zip the recap's goals[] and statfeeds[] in the frontend rather than adding a new
  // backend timeline DTO; the data is identical, ordering is the only derived concern.
  template: `
    <div class="timeline">
      <h3 class="timeline__title">Event Timeline</h3>
      <ol class="timeline__list">
        @for (entry of entries(); track entry.id) {
          <li class="timeline__row"
              [class.timeline__row--goal]="entry.kind === 'goal'"
              [class.timeline__row--blue]="teamFor(entry) === 'blue'"
              [class.timeline__row--orange]="teamFor(entry) === 'orange'">
            <span class="timeline__clock">{{ entry.matchClockSeconds | duration }}</span>
            @if (entry.kind === 'goal' && entry.goal) {
              @let g = entry.goal;
              <span class="timeline__icon" aria-hidden="true">⚽</span>
              <span class="timeline__text">
                <strong>{{ g.scorer.name }}</strong> scored
                @if (g.assister) {
                  <span class="timeline__sub"> (assist: {{ g.assister.name }})</span>
                }
                <span class="timeline__score">[{{ g.blueScoreAfter }}–{{ g.orangeScoreAfter }}]</span>
              </span>
              <span class="timeline__meta">{{ g.goalSpeedUuPerSec | kmh }}</span>
            }
            @if (entry.kind === 'statfeed' && entry.statfeed) {
              @let sf = entry.statfeed;
              <span class="timeline__icon" aria-hidden="true">{{ sf.type | statfeedIcon }}</span>
              <span class="timeline__text">
                <strong>{{ sf.mainTarget.name }}</strong> — {{ sf.displayName }}
                @if (sf.secondaryTarget) {
                  <span class="timeline__sub"> on {{ sf.secondaryTarget.name }}</span>
                }
              </span>
            }
          </li>
        } @empty {
          <li class="timeline__empty">No events recorded for this match.</li>
        }
      </ol>
    </div>
  `,
  styles: [`
    :host { grid-column: 1 / -1; }
    .timeline { padding: 1rem 1.5rem; }
    .timeline__title { font-family: var(--font-header); font-size: var(--text-base); color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.08em; margin: 0 0 0.75rem; }
    .timeline__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.25rem; max-height: 28rem; overflow-y: auto; }
    .timeline__row {
      display: grid;
      grid-template-columns: 4rem auto 1fr auto;
      align-items: center;
      gap: 0.5rem;
      padding: 0.375rem 0.75rem;
      font-family: var(--font-body);
      font-size: var(--text-sm);
      border-left: 3px solid var(--text-muted);
      border-radius: 2px;
      background: var(--bg-overlay);
    }
    .timeline__row--goal { background: color-mix(in srgb, var(--bg-overlay) 88%, var(--accent-mvp) 12%); }
    .timeline__row--blue { border-left-color: var(--team-blue); }
    .timeline__row--orange { border-left-color: var(--team-orange); }
    .timeline__clock { font-family: var(--font-display); font-size: var(--text-xs); color: var(--text-muted); }
    .timeline__icon { font-size: var(--text-base); }
    .timeline__text { color: var(--text-primary); }
    .timeline__sub { color: var(--text-secondary); }
    .timeline__score { color: var(--text-muted); margin-left: 0.5rem; font-family: var(--font-display); }
    .timeline__meta { color: var(--text-muted); font-family: var(--font-display); font-size: var(--text-xs); white-space: nowrap; }
    .timeline__empty { padding: 1rem; color: var(--text-muted); text-align: center; font-size: var(--text-sm); }
  `],
})
export class EventTimelineComponent {
  readonly recap = input.required<MatchRecap>();

  protected readonly entries = computed<TimelineEntry[]>(() => {
    const r = this.recap();
    const goals: TimelineEntry[] = r.goals.map(g => ({
      id: `g-${g.id}`,
      kind: 'goal',
      matchClockSeconds: g.matchClockSeconds,
      goal: g,
    }));
    const sfs: TimelineEntry[] = r.statfeeds.map((s, i) => ({
      id: `sf-${s.timestamp}-${i}`,
      kind: 'statfeed',
      matchClockSeconds: s.matchClockSeconds,
      statfeed: s,
    }));
    // Stable secondary order (timestamp tie-breaker) keeps the AerialGoal/BackwardsGoal/etc
    // qualifier statfeeds adjacent to their goal.
    return [...goals, ...sfs].sort((a, b) => {
      const dt = a.matchClockSeconds - b.matchClockSeconds;
      if (dt !== 0) return dt;
      const at = a.kind === 'goal' ? a.goal!.timestamp : a.statfeed!.timestamp;
      const bt = b.kind === 'goal' ? b.goal!.timestamp : b.statfeed!.timestamp;
      return at.localeCompare(bt);
    });
  });

  protected teamFor(entry: TimelineEntry): 'blue' | 'orange' | 'unknown' {
    if (entry.kind === 'goal' && entry.goal) {
      return entry.goal.scorer.team;
    }
    if (entry.kind === 'statfeed' && entry.statfeed) {
      return entry.statfeed.mainTarget.team;
    }
    return 'unknown';
  }
}
