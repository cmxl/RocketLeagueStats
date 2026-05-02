import { PlayerRef } from './player';
import { StatfeedType } from './enums';

export interface Statfeed {
  timestamp: string;
  matchClockSeconds: number;
  /** Stable enum bucket — use for filtering, switching, aggregation. */
  type: StatfeedType;
  /** RL's verbatim human label (e.g. "Ultra Damage", "Bicycle Hit") — for UI rendering. */
  displayName: string;
  mainTarget: PlayerRef;
  secondaryTarget: PlayerRef | null;
}
