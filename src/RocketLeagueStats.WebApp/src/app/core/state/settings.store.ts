import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withHooks, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { from, pipe, switchMap, tap } from 'rxjs';
import { ApiClient } from '../api/api-client.service';
import { Settings } from '../models';

interface SettingsState {
  loaded: Settings | null;
  draft: Settings | null;
  saveStatus: 'idle' | 'saving' | 'error';
}

const empty: Settings = { playerName: null, friendNames: [], showTrainingInHistory: false };

export const SettingsStore = signalStore(
  { providedIn: 'root' },
  withState<SettingsState>({ loaded: null, draft: null, saveStatus: 'idle' }),
  withComputed(({ loaded, draft }) => ({
    current: computed(() => draft() ?? loaded() ?? empty),
    hasUnsavedChanges: computed(() => draft() !== null && JSON.stringify(draft()) !== JSON.stringify(loaded())),
  })),
  withMethods((store) => {
    const api = inject(ApiClient);

    const setDraft = (patch: Partial<Settings>) => patchState(store, (s) => ({
      draft: { ...(s.draft ?? s.loaded ?? empty), ...patch },
    }));

    const cancel = () => patchState(store, { draft: null });

    const save = rxMethod<void>(pipe(
      tap(() => patchState(store, { saveStatus: 'saving' })),
      switchMap(() => from(api.updateSettings(store.draft()!))),
      tap((saved) => patchState(store, { loaded: saved, draft: null, saveStatus: 'idle' })),
    ));

    const load = rxMethod<void>(pipe(
      switchMap(() => from(api.getSettings())),
      tap((s) => patchState(store, { loaded: s })),
    ));

    return { setDraft, cancel, save, load };
  }),
  withHooks({
    onInit(store) { store.load(); },
  }),
);
