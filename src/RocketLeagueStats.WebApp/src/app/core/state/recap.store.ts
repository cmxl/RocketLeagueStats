import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';

interface RecapState { matchId: string | null; }

export const RecapStore = signalStore(
  { providedIn: 'root' },
  withState<RecapState>({ matchId: null }),
  withMethods((store) => ({
    load(matchId: string) { patchState(store, { matchId }); },
    clear() { patchState(store, { matchId: null }); },
  })),
);
