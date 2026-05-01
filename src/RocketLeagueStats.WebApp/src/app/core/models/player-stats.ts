import { PlayerRef } from './player';

export interface PlayerStatsRow {
  player: PlayerRef;
  goals: number;
  assists: number;
  saves: number;
  epicSaves: number;
  shots: number;
  demosInflicted: number;
  demosTaken: number;
  crossbarHits: number;
  fastestGoalSpeedUuPerSec: number;
  mvpScore: number;
  isMvp: boolean;
}
