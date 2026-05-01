import { Team } from './enums';

export interface PlayerRef {
  name: string;
  shortcut: number;
  team: Team;
}

export interface Vec3 { x: number; y: number; z: number; }
