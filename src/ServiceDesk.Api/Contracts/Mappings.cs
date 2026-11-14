using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Contracts;

/// <summary>Maps domain entities to API contracts so entities are never exposed directly.</summary>
public static class Mappings
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.FullName, user.Email, user.Role.ToString());

    public static PersonDto ToPerson(this User user) => new(user.Id, user.FullName);

    public static CommentDto ToDto(this TicketComment comment) =>
        new(comment.Id, comment.Author.ToPerson(), comment.Body, comment.CreatedAtUtc);

    public static TicketSummaryDto ToSummaryDto(this Ticket ticket, DateTime nowUtc) =>
        new(ticket.Id, ticket.Title, ticket.Status, ticket.Priority,
            ticket.CreatedBy.ToPerson(), ticket.AssignedTo?.ToPerson(),
            ticket.CreatedAtUtc, ticket.DueAtUtc, ticket.IsOverdue(nowUtc));

    public static TicketDetailDto ToDetailDto(this Ticket ticket, DateTime nowUtc) =>
        new(ticket.Id, ticket.Title, ticket.Description, ticket.Status, ticket.Priority,
            ticket.CreatedBy.ToPerson(), ticket.AssignedTo?.ToPerson(),
            ticket.CreatedAtUtc, ticket.UpdatedAtUtc, ticket.DueAtUtc, ticket.ResolvedAtUtc,
            ticket.IsOverdue(nowUtc),
            TicketWorkflow.NextStatuses(ticket.Status),
            ticket.Comments.OrderBy(c => c.CreatedAtUtc).Select(c => c.ToDto()).ToList());
}
