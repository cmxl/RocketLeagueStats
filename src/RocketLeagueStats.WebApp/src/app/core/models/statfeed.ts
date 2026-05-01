import { PlayerRef } from './player';
import { StatfeedType } from './enums';

export interface Statfeed {
  timestamp: string;
  matchClockSeconds: number;
  type: StatfeedType;
  mainTarget: PlayerRef;
  secondaryTarget: PlayerRef | null;
}
