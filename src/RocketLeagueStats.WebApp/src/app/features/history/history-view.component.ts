import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { HistoryStore } from '../../core/state/history.store';
import { MatchSummary } from '../../core/models/match';
import { FilterBarComponent } from './filter-bar.component';
import { MatchCardComponent } from './match-card.component';
import { SummaryStripComponent } from './summary-strip.component';

@Component({
  selector: 'rls-history-view',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FilterBarComponent, MatchCardComponent, SummaryStripComponent],
  template: `
    <rls-filter-bar />
    <rls-summary-strip [matches]="matches.value() ?? []" />
    @if (matches.isLoading()) {
      <div class="history-loading">Loading matches…</div>
    } @else if (matches.error()) {
      <div class="history-error">Failed to load matches.</div>
    } @else {
      <section class="history-grid">
        @for (match of (matches.value() ?? []); track match.matchId) {
          <rls-match-card [match]="match" />
        } @empty {
          <p class="history-empty">No matches yet.</p>
        }
      </section>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; }
    .history-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 1rem;
      padding: 1.5rem;
    }
    .history-loading, .history-error, .history-empty {
      padding: 2rem 1.5rem;
      color: var(--text-secondary);
      font-family: var(--font-header);
      text-align: center;
    }
    .history-error { color: var(--accent-danger); }
  `],
})
export class HistoryViewComponent {
  private readonly store = inject(HistoryStore);

  protected readonly matches = httpResource<MatchSummary[]>(() => ({
    url: '/api/matches',
    params: {
      includeTraining: String(this.store.filter().includeTraining),
      includeFreePlay: String(this.store.filter().includeFreePlay),
      sort: this.store.filter().sort,
    },
  }));
}
