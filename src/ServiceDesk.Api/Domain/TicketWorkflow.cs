namespace ServiceDesk.Api.Domain;

/// <summary>
/// The allowed ticket lifecycle:
/// Open -> InProgress -> Resolved -> Closed, with reopen (Resolved -> InProgress),
/// hand-back (InProgress -> Open) and early close (Open -> Closed).
/// </summary>
public static class TicketWorkflow
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Allowed =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Closed],
            [TicketStatus.InProgress] = [TicketStatus.Resolved, TicketStatus.Open],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
            [TicketStatus.Closed] = []
        };

    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static IReadOnlyList<TicketStatus> NextStatuses(TicketStatus from) => Allowed[from];
}
