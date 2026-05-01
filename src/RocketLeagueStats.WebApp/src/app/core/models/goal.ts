import { PlayerRef, Vec3 } from './player';

export interface Goal {
  id: string;
  timestamp: string;            // ISO-8601 from server
  matchClockSeconds: number;
  scorer: PlayerRef;
  assister: PlayerRef | null;
  goalSpeedUuPerSec: number;
  impactLocation: Vec3;
  blueScoreAfter: number;
  orangeScoreAfter: number;
  secondsSinceLastGoal: number | null;
}
