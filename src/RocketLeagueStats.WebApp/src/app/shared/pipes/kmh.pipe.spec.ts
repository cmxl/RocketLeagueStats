import { KmhPipe } from './kmh.pipe';

describe('KmhPipe', () => {
  const pipe = new KmhPipe();

  // Real-capture sample: a 125.24 km/h ranked goal — Psyonix's "GoalSpeed"
  // is already km/h, so the pipe just rounds and labels.
  it('rounds and labels a real-game speed', () => expect(pipe.transform(125.24)).toBe('125 km/h'));

  it('rounds half-up for typical values', () => expect(pipe.transform(94.6)).toBe('95 km/h'));

  it('handles zero (kickoff phantom or own-goal edge case)', () => expect(pipe.transform(0)).toBe('0 km/h'));

  it('handles null', () => expect(pipe.transform(null)).toBe('—'));
});
