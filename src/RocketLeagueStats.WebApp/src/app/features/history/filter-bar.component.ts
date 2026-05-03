import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { HistoryStore } from '../../core/state/history.store';
import { HistorySort } from '../../core/models/enums';

@Component({
  selector: 'rls-filter-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filter-bar">
      <div class="filter-bar__sort">
        <label class="sort-label" for="sort-select">Sort:</label>
        <select id="sort-select" class="sort-select"
                [value]="store.filter().sort"
                (change)="onSortChange($event)">
          <option value="mostRecent">Most Recent</option>
          <option value="highestScoring">Highest Scoring</option>
        </select>
      </div>
    </div>
  `,
  styles: [`
    .filter-bar {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      padding: 0.75rem 1.5rem;
      background: var(--bg-elevated);
      border-bottom: 1px solid var(--text-muted);
      flex-wrap: wrap;
    }
    .filter-bar__sort { display: flex; align-items: center; gap: 0.5rem; margin-left: auto; }
    .sort-label { font-family: var(--font-header); font-size: var(--text-xs); color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.06em; }
    .sort-select {
      background: var(--bg-overlay);
      border: 1px solid var(--text-muted);
      color: var(--text-primary);
      font-family: var(--font-header);
      font-size: var(--text-xs);
      padding: 0.25rem 0.5rem;
      border-radius: 2px;
      cursor: pointer;
    }
    .sort-select:focus { outline: 1px solid var(--accent-cyan); }
  `],
})
export class FilterBarComponent {
  protected readonly store = inject(HistoryStore);

  protected onSortChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as HistorySort;
    this.store.setFilter({ sort: value });
  }
}
