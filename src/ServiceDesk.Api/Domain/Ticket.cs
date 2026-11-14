using ServiceDesk.Api.Common;

namespace ServiceDesk.Api.Domain;

/// <summary>
/// A support ticket. State changes go through methods so business rules
/// (workflow, assignment, comments) are enforced in one place.
/// </summary>
public class Ticket
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; }

    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public int? AssignedToId { get; private set; }
    public User? AssignedTo { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    public List<TicketComment> Comments { get; } = [];

    public static Ticket Create(
        string title, string description, TicketPriority priority,
        User createdBy, DateTime nowUtc, DateTime dueAtUtc) => new()
    {
        Title = title.Trim(),
        Description = description.Trim(),
        Priority = priority,
        CreatedBy = createdBy,
        CreatedById = createdBy.Id,
        CreatedAtUtc = nowUtc,
        UpdatedAtUtc = nowUtc,
        DueAtUtc = dueAtUtc
    };

    public bool IsOverdue(DateTime nowUtc) =>
        (Status is TicketStatus.Open or TicketStatus.InProgress) && nowUtc > DueAtUtc;

    public void ChangeStatus(TicketStatus next, DateTime nowUtc)
    {
        if (next == Status)
        {
            return;
        }

        if (!TicketWorkflow.CanTransition(Status, next))
        {
            throw new DomainRuleException($"Cannot move a ticket from {Status} to {next}.");
        }

        if (next == TicketStatus.Resolved)
        {
            ResolvedAtUtc = nowUtc;
        }
        else if (Status == TicketStatus.Resolved && next == TicketStatus.InProgress)
        {
            ResolvedAtUtc = null; // reopened
        }

        Status = next;
        UpdatedAtUtc = nowUtc;
    }

    public void AssignTo(User agent, DateTime nowUtc)
    {
        if (!agent.CanWorkTickets)
        {
            throw new DomainRuleException("Tickets can only be assigned to agents or admins.");
        }

        if (Status == TicketStatus.Closed)
        {
            throw new DomainRuleException("Closed tickets cannot be reassigned.");
        }

        AssignedToId = agent.Id;
        AssignedTo = agent;
        UpdatedAtUtc = nowUtc;
    }

    public TicketComment AddComment(User author, string body, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new DomainRuleException("Closed tickets cannot receive new comments.");
        }

        var comment = new TicketComment
        {
            Author = author,
            AuthorId = author.Id,
            Body = body.Trim(),
            CreatedAtUtc = nowUtc
        };

        Comments.Add(comment);
        UpdatedAtUtc = nowUtc;
        return comment;
    }
}
