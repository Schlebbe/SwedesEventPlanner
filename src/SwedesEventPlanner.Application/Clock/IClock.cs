namespace SwedesEventPlanner.Application.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
