using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Services.Sla;

/// <summary>
/// Strategy pattern: each priority has its own SLA policy. New priorities or
/// different targets are added by registering another policy, not by editing a switch.
/// </summary>
public interface ISlaPolicy
{
    TicketPriority Priority { get; }
    TimeSpan ResolutionTarget { get; }
}

public sealed class CriticalSlaPolicy : ISlaPolicy
{
    public TicketPriority Priority => TicketPriority.Critical;
    public TimeSpan ResolutionTarget => TimeSpan.FromHours(4);
}

public sealed class HighSlaPolicy : ISlaPolicy
{
    public TicketPriority Priority => TicketPriority.High;
    public TimeSpan ResolutionTarget => TimeSpan.FromHours(8);
}

public sealed class MediumSlaPolicy : ISlaPolicy
{
    public TicketPriority Priority => TicketPriority.Medium;
    public TimeSpan ResolutionTarget => TimeSpan.FromHours(24);
}

public sealed class LowSlaPolicy : ISlaPolicy
{
    public TicketPriority Priority => TicketPriority.Low;
    public TimeSpan ResolutionTarget => TimeSpan.FromHours(72);
}

public interface ISlaCalculator
{
    DateTime CalculateDueDate(TicketPriority priority, DateTime createdAtUtc);
}

public sealed class SlaCalculator(IEnumerable<ISlaPolicy> policies) : ISlaCalculator
{
    private readonly IReadOnlyDictionary<TicketPriority, ISlaPolicy> _policies =
        policies.ToDictionary(p => p.Priority);

    public DateTime CalculateDueDate(TicketPriority priority, DateTime createdAtUtc) =>
        _policies.TryGetValue(priority, out var policy)
            ? createdAtUtc.Add(policy.ResolutionTarget)
            : throw new InvalidOperationException($"No SLA policy is registered for priority {priority}.");
}
