export type Team = 'blue' | 'orange' | 'unknown';
export type MatchPhase = 'idle' | 'live';

export type MatchType =
  | 'unknown' | 'ranked1v1' | 'ranked2v2' | 'ranked3v3'
  | 'casual' | 'tournament' | 'private' | 'freePlay' | 'training'
  | 'online' | 'offline';   // coarse fallbacks until snapshot parsing refines them

export type StatfeedType =
  | 'other' | 'save' | 'epicSave' | 'demolish' | 'hattrick' | 'mvpHattrick'
  | 'savior' | 'bicycleHit' | 'damage' | 'ultraDamage'
  | 'aerialGoal' | 'backwardsGoal' | 'overtimeGoal' | 'mvp' | 'win';

export type HistorySort = 'mostRecent' | 'highestScoring';
