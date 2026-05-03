import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { HistorySort } from '../models';

// Training / free-play / private-match events are dropped at the SQLite write layer
// (SqliteEventStoreService skips empty-MatchGuid events) and at the read layer
// (MatchHistoryReader filters out empty MatchGuid). The history page therefore has nothing
// for the training/freePlay toggles to act on, so they're gone from the UI as of #2 — the
// filter shape is now sort-only. The /api/matches endpoint still accepts the legacy
// includeTraining/includeFreePlay query params for any other caller that wants them.
interface HistoryFilter {
  sort: HistorySort;
}

interface HistoryState { filter: HistoryFilter; }

export const HistoryStore = signalStore(
  { providedIn: 'root' },
  withState<HistoryState>({
    filter: { sort: 'mostRecent' },
  }),
  withMethods((store) => ({
    setFilter(patch: Partial<HistoryFilter>) {
      patchState(store, (s) => ({ filter: { ...s.filter, ...patch } }));
    },
  })),
);
