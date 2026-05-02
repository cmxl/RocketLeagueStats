import { Team } from './enums';

export interface PlayerRef {
  name: string;
  shortcut: number;
  team: Team;
  // Empty until the first MatchStateSnapshot tick of a match — discrete events (goals/statfeeds)
  // don't carry the platform on the wire, only the snapshot does.
  platform: string;
}

export interface Vec3 { x: number; y: number; z: number; }
