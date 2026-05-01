export interface Settings {
  playerName: string | null;
  friendNames: string[];
  showTrainingInHistory: boolean;
}

export interface ServerInfo {
  version: string;
  buildDate: string;
  enabledFeatures: string[];
}
