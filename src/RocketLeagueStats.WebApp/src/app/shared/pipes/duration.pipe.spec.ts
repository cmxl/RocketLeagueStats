import { DurationPipe } from './duration.pipe';

describe('DurationPipe', () => {
  const pipe = new DurationPipe();
  it('formats seconds as M:SS', () => expect(pipe.transform(125)).toBe('2:05'));
  it('handles null', () => expect(pipe.transform(null)).toBe('—'));
});
