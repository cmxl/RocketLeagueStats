import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import {
  Goal, Statfeed, MatchHeader, MatchSummary, ConnectionState, MatchPhase, PlayerStatsRow,
} from '../models';

export type HubState = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

@Injectable({ providedIn: 'root' })
export class StatsHubClient {
  private connection: HubConnection | null = null;
  readonly state = signal<HubState>('idle');

  private readonly reconnectedFns: Array<() => void> = [];

  async connect(): Promise<void> {
    if (this.connection) return;

    this.connection = new HubConnectionBuilder()
      .withUrl('/hub/stats')
      .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.onreconnecting(() => this.state.set('reconnecting'));
    this.connection.onreconnected(() => {
      this.state.set('connected');
      for (const fn of this.reconnectedFns) fn();
    });
    this.connection.onclose(() => this.state.set('disconnected'));

    this.state.set('connecting');
    await this.connection.start();
    this.state.set('connected');
  }

  onGoal(fn: (g: Goal) => void) { this.connection?.on('OnGoal', fn); }
  onStatfeed(fn: (s: Statfeed) => void) { this.connection?.on('OnStatfeed', fn); }
  onClockTick(fn: (sec: number) => void) { this.connection?.on('OnClockTick', fn); }
  onPlayerStatsTick(fn: (rows: PlayerStatsRow[]) => void) { this.connection?.on('OnPlayerStatsTick', fn); }
  onPhaseChanged(fn: (p: MatchPhase) => void) { this.connection?.on('OnPhaseChanged', fn); }
  onConnectionState(fn: (c: ConnectionState) => void) { this.connection?.on('OnConnectionState', fn); }
  onMatchInitialized(fn: (h: MatchHeader) => void) { this.connection?.on('OnMatchInitialized', fn); }
  onRosterUpdated(fn: (h: MatchHeader) => void) { this.connection?.on('OnRosterUpdated', fn); }
  onMatchEnded(fn: (s: MatchSummary) => void) { this.connection?.on('OnMatchEnded', fn); }

  onReconnected(fn: () => void): void { this.reconnectedFns.push(fn); }
}
