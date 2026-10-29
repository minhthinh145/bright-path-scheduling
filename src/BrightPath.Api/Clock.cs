namespace BrightPath.Api;

public interface IClock
{
    DateTime Now { get; }
}

public class PinnedClock : IClock
{
    private readonly DateTime _now;
    public PinnedClock(DateTime now) => _now = now;
    public DateTime Now => _now;
}
