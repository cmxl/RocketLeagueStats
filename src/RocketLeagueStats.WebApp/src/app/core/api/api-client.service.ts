import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  LiveState, MatchSummary, MatchRecap, Settings, ServerInfo, HistorySort,
} from '../models';

export interface MatchHistoryQueryParams {
  includeTraining?: boolean;
  includeFreePlay?: boolean;
  from?: string;
  to?: string;
  sort?: HistorySort;
}

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  getState(): Promise<LiveState> {
    return firstValueFrom(this.http.get<LiveState>('/api/state'));
  }

  getMatches(params: MatchHistoryQueryParams = {}): Promise<MatchSummary[]> {
    let q = new HttpParams();
    if (params.includeTraining !== undefined) q = q.set('includeTraining', String(params.includeTraining));
    if (params.includeFreePlay !== undefined) q = q.set('includeFreePlay', String(params.includeFreePlay));
    if (params.from) q = q.set('from', params.from);
    if (params.to) q = q.set('to', params.to);
    if (params.sort) q = q.set('sort', params.sort);
    return firstValueFrom(this.http.get<MatchSummary[]>('/api/matches', { params: q }));
  }

  getMatchRecap(id: string): Promise<MatchRecap> {
    return firstValueFrom(this.http.get<MatchRecap>(`/api/matches/${encodeURIComponent(id)}`));
  }

  deleteMatch(id: string): Promise<void> {
    // Server returns 204 No Content on success and 404 when the MatchGuid isn't found. The
    // cascade across Events / MatchSnapshots / EventParticipants / PlayerMatchStats happens
    // server-side via SQLite FK cascades.
    return firstValueFrom(this.http.delete<void>(`/api/matches/${encodeURIComponent(id)}`));
  }

  getSettings(): Promise<Settings> {
    return firstValueFrom(this.http.get<Settings>('/api/settings'));
  }

  updateSettings(settings: Settings): Promise<Settings> {
    return firstValueFrom(this.http.put<Settings>('/api/settings', settings));
  }

  getInfo(): Promise<ServerInfo> {
    return firstValueFrom(this.http.get<ServerInfo>('/api/info'));
  }
}
