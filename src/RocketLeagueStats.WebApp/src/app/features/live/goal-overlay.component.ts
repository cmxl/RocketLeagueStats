import { Component, ChangeDetectionStrategy, inject, effect } from '@angular/core';
import { LiveMatchStore } from '../../core/state/live-match.store';
import { KmhPipe } from '../../shared/pipes/kmh.pipe';

@Component({
  selector: 'rls-goal-overlay',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KmhPipe],
  template: `
    @let goal = live.pendingGoalOverlay();
    @if (goal) {
      <div class="goal-overlay"
           [class.goal-overlay--blue]="goal.scorer.team === 'blue'"
           [class.goal-overlay--orange]="goal.scorer.team === 'orange'">
        <span class="goal-overlay__kicker">GOAL</span>
        <span class="goal-overlay__scorer">{{ goal.scorer.name }}</span>
        @if (goal.assister) {
          <span class="goal-overlay__assist">assist by {{ goal.assister.name }}</span>
        }
        <span class="goal-overlay__speed">{{ goal.goalSpeedUuPerSec | kmh }}</span>
      </div>
    }
  `,
  styles: [`
    .goal-overlay {
      position: fixed;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      background: rgba(0, 0, 0, 0.75);
      z-index: 500;
      animation: rls-goal-in 400ms ease forwards;
    }
    .goal-overlay--blue { border-top: 6px solid var(--team-blue); }
    .goal-overlay--orange { border-top: 6px solid var(--team-orange); }
    .goal-overlay__kicker {
      font-family: var(--font-display);
      font-size: var(--text-display-lg);
      line-height: 1;
      color: var(--accent-mvp);
      letter-spacing: 0.1em;
    }
    .goal-overlay__scorer {
      font-family: var(--font-display);
      font-size: var(--text-display-md);
      line-height: 1;
      color: var(--text-primary);
    }
    .goal-overlay__assist {
      font-family: var(--font-header);
      font-size: var(--text-xl);
      color: var(--text-secondary);
      margin-top: 0.5rem;
    }
    .goal-overlay__speed {
      font-family: var(--font-header);
      font-size: var(--text-lg);
      color: var(--text-muted);
      margin-top: 0.25rem;
    }
  `],
})
export class GoalOverlayComponent {
  protected readonly live = inject(LiveMatchStore);

  constructor() {
    effect((onCleanup) => {
      if (this.live.pendingGoalOverlay() !== null) {
        const t = setTimeout(() => this.live.dismissGoalOverlay(), 3_500);
        onCleanup(() => clearTimeout(t));
      }
    });
  }
}
