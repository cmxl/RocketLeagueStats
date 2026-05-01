import { Component, ChangeDetectionStrategy, inject, signal, computed, OnDestroy } from '@angular/core';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { DurationPipe } from '../../shared/pipes/duration.pipe';

@Component({
  selector: 'rls-time-since-goal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DurationPipe],
  template: `
    @if (seconds() != null) {
      <div class="time-since-goal">
        <span class="time-since-goal__label">Time since last goal</span>
        <span class="time-since-goal__value">{{ seconds()! | duration }}</span>
      </div>
    }
  `,
  styles: [`
    .time-since-goal {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.5rem 1.5rem;
      background: var(--bg-overlay);
      border-top: 1px solid var(--text-muted);
      font-family: var(--font-header);
    }
    .time-since-goal__label { font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.08em; }
    .time-since-goal__value { font-family: var(--font-display); font-size: var(--text-lg); color: var(--text-primary); }
  `],
})
export class TimeSinceGoalComponent implements OnDestroy {
  private readonly live = inject(LiveMatchStore);
  private readonly tick = signal(Date.now());
  private readonly timer = setInterval(() => this.tick.set(Date.now()), 1000);

  protected readonly seconds = computed(() => {
    const last = this.live.lastGoalAt();
    if (!last) return null;
    return Math.floor((this.tick() - last.getTime()) / 1000);
  });

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }
}
