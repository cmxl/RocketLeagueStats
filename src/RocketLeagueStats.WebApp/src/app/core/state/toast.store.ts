import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { MatchSummary } from '../models';

interface ToastState { matchEndedToast: MatchSummary | null; }

export const ToastStore = signalStore(
  { providedIn: 'root' },
  withState<ToastState>({ matchEndedToast: null }),
  withMethods((store) => ({
    showMatchEndedToast(summary: MatchSummary) {
      patchState(store, { matchEndedToast: summary });
      setTimeout(() => patchState(store, { matchEndedToast: null }), 30_000);
    },
    dismiss() {
      patchState(store, { matchEndedToast: null });
    },
  })),
);
