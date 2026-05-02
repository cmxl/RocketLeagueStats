import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withHooks, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { from, pipe, switchMap, tap } from 'rxjs';
import { ApiClient } from '../api/api-client.service';
import { StatsHubClient } from '../api/stats-hub.client';
import {
  Goal, Statfeed, MatchHeader, MatchSummary, MatchPhase, PlayerStatsRow, ConnectionState,
} from '../models';
import { ToastStore } from './toast.store';

interface LiveMatchState {
  phase: MatchPhase;
  currentMatch: MatchHeader | null;
  clockSeconds: number | null;
  blueScore: number;
  orangeScore: number;
  playerStats: PlayerStatsRow[];
  goals: Goal[];
  statfeeds: Statfeed[];
  lastGoalAt: Date | null;
  gameConnected: boolean;
  pendingGoalOverlay: Goal | null;
}

const initialState: LiveMatchState = {
  phase: 'idle',
  currentMatch: null,
  clockSeconds: null,
  blueScore: 0,
  orangeScore: 0,
  playerStats: [],
  goals: [],
  statfeeds: [],
  lastGoalAt: null,
  gameConnected: true,
  pendingGoalOverlay: null,
};

export const LiveMatchStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ phase, currentMatch }) => ({
    hasLiveMatch: computed(() => phase() === 'live' && !!currentMatch()),
  })),
  withMethods((store) => {
    const api = inject(ApiClient);
    const hub = inject(StatsHubClient);
    const toast = inject(ToastStore);

    // Goals and statfeeds are uncapped — the live view keeps the entire match history so the
    // action feed can scroll back to the start. New events go on top (newest-first), matching
    // the server-side LiveMatchState.
    const handleGoal = (g: Goal) => patchState(store, (s) => ({
      goals: [g, ...s.goals],
      blueScore: g.blueScoreAfter,
      orangeScore: g.orangeScoreAfter,
      lastGoalAt: new Date(g.timestamp),
      pendingGoalOverlay: g,
    }));

    const dismissGoalOverlay = () => patchState(store, { pendingGoalOverlay: null });

    const handleStatfeed = (sf: Statfeed) => patchState(store, (s) => ({
      statfeeds: [sf, ...s.statfeeds],
    }));

    const refreshFromServer = rxMethod<void>(pipe(
      switchMap(() => from(api.getState())),
      tap((state) => patchState(store, {
        phase: state.phase,
        currentMatch: state.currentMatch,
        clockSeconds: state.currentMatchClockSeconds,
        blueScore: state.blueScore,
        orangeScore: state.orangeScore,
        playerStats: state.playerStats,
        goals: state.goals,
        statfeeds: state.statfeeds,
        lastGoalAt: state.lastGoalAt ? new Date(state.lastGoalAt) : null,
        gameConnected: state.connection.connectedToGame,
      })),
    ));

    return { handleGoal, dismissGoalOverlay, handleStatfeed, refreshFromServer };
  }),
  withHooks({
    onInit(store) {
      const hub = inject(StatsHubClient);
      const toast = inject(ToastStore);

      hub.connect().then(() => {
        hub.onGoal((g) => store.handleGoal(g));
        hub.onStatfeed((s) => store.handleStatfeed(s));
        hub.onClockTick((sec) => patchState(store, { clockSeconds: sec }));
        hub.onPlayerStatsTick((rows) => patchState(store, { playerStats: rows }));
        hub.onPhaseChanged((p) => patchState(store, { phase: p }));
        hub.onConnectionState((c: ConnectionState) =>
          patchState(store, { gameConnected: c.connectedToGame }));
        hub.onMatchInitialized((h) => patchState(store, {
          currentMatch: h, blueScore: 0, orangeScore: 0,
          playerStats: [], goals: [], statfeeds: [],
          clockSeconds: 0, lastGoalAt: null, pendingGoalOverlay: null,
        }));
        // Roster grew mid-match (server discovered a new player from a goal/statfeed event).
        // Patch only the header — preserve scores, feeds, clocks, and overlays.
        hub.onRosterUpdated((h) => patchState(store, { currentMatch: h }));
        hub.onMatchEnded((sum: MatchSummary) => toast.showMatchEndedToast(sum));
        hub.onReconnected(() => store.refreshFromServer());

        store.refreshFromServer();
      });
    },
  }),
);
