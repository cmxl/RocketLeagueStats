import { MatchPhase } from './enums';
import { MatchHeader } from './match';
import { PlayerStatsRow } from './player-stats';
import { Goal } from './goal';
import { Statfeed } from './statfeed';

export interface ConnectionState {
  connectedToGame: boolean;
  lastEventReceivedAt: string | null;
}

export interface LiveState {
  phase: MatchPhase;
  currentMatch: MatchHeader | null;
  currentMatchClockSeconds: number | null;
  blueScore: number;
  orangeScore: number;
  playerStats: PlayerStatsRow[];
  /** Full match history of goals, newest first. */
  goals: Goal[];
  /** Full match history of statfeed events, newest first. */
  statfeeds: Statfeed[];
  lastGoalAt: string | null;
  connection: ConnectionState;
}
