namespace RocketLeagueStats.Core.Bus;

internal sealed class SubscriberDropTracker(int subscriberId)
{
    private long drops;

    public int SubscriberId => subscriberId;

    public void Increment() => Interlocked.Increment(ref this.drops);

    public long Snapshot() => Interlocked.Exchange(ref this.drops, 0);
}
