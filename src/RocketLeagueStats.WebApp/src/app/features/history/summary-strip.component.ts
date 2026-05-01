import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { MatchSummary } from '../../core/models/match';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-summary-strip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DurationPipe],
  template: `
    @if (matches().length > 0) {
      <div class="summary-strip">
        <div class="summary-stat">
          <span class="summary-stat__number">{{ matches().length }}</span>
          <span class="summary-stat__label">Matches</span>
        </div>
        <div class="summary-stat">
          <span class="summary-stat__number">{{ totalGoals() }}</span>
          <span class="summary-stat__label">Total Goals</span>
        </div>
        <div class="summary-stat">
          <span class="summary-stat__number">{{ avgDuration() | duration }}</span>
          <span class="summary-stat__label">Avg Duration</span>
        </div>
      </div>
    }
  `,
  styles: [`
    .summary-strip {
      display: flex;
      gap: 2rem;
      padding: 0.75rem 1.5rem;
      background: var(--bg-overlay);
      border-bottom: 1px solid var(--text-muted);
    }
    .summary-stat { display: flex; flex-direction: column; align-items: center; gap: 0.1rem; }
    .summary-stat__number { font-family: var(--font-display); font-size: var(--text-2xl); color: var(--accent-cyan); line-height: 1; }
    .summary-stat__label { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; }
  `],
})
export class SummaryStripComponent {
  readonly matches = input.required<MatchSummary[]>();

  protected readonly totalGoals = computed(() =>
    this.matches().reduce((sum, m) => sum + m.totalGoals, 0),
  );

  protected readonly avgDuration = computed(() => {
    const list = this.matches();
    if (list.length === 0) return 0;
    const total = list.reduce((sum, m) => sum + m.durationSeconds, 0);
    return Math.floor(total / list.length);
  });
}
