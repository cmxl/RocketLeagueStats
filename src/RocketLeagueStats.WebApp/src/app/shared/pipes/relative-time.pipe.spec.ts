import { RelativeTimePipe } from './relative-time.pipe';

describe('RelativeTimePipe', () => {
  const pipe = new RelativeTimePipe();
  it('returns — for null', () => expect(pipe.transform(null)).toBe('—'));
  it('shows seconds ago for recent dates', () => {
    const recent = new Date(Date.now() - 30_000);
    expect(pipe.transform(recent)).toBe('30s ago');
  });
});
