import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { HistorySort } from '../models';

interface HistoryFilter {
  includeTraining: boolean;
  includeFreePlay: boolean;
  sort: HistorySort;
}

interface HistoryState { filter: HistoryFilter; }

export const HistoryStore = signalStore(
  { providedIn: 'root' },
  withState<HistoryState>({
    filter: { includeTraining: false, includeFreePlay: false, sort: 'mostRecent' },
  }),
  withMethods((store) => ({
    setFilter(patch: Partial<HistoryFilter>) {
      patchState(store, (s) => ({ filter: { ...s.filter, ...patch } }));
    },
  })),
);
