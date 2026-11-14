using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Contracts;

// ---------- Auth ----------

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

public sealed record UserDto(int Id, string FullName, string Email, string Role);

public sealed record PersonDto(int Id, string FullName);

// ---------- Tickets ----------

public sealed record CreateTicketRequest(
    [Required, StringLength(200, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Description,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority);

public sealed record AssignTicketRequest([Range(1, int.MaxValue)] int AgentId);

public sealed record ChangeStatusRequest([EnumDataType(typeof(TicketStatus))] TicketStatus Status);

public sealed record AddCommentRequest([Required, StringLength(2000, MinimumLength = 1)] string Body);

/// <summary>Query-string filters for GET /api/tickets.</summary>
public sealed class TicketQuery
{
    public TicketStatus? Status { get; init; }
    public TicketPriority? Priority { get; init; }

    /// <summary>Matches title or description.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    /// <summary>Only tickets assigned to the caller (agents and admins).</summary>
    public bool? AssignedToMe { get; init; }

    /// <summary>Only open or in-progress tickets past their SLA due date.</summary>
    public bool? OverdueOnly { get; init; }

    [Range(1, 10_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record TicketSummaryDto(
    int Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    PersonDto CreatedBy,
    PersonDto? AssignedTo,
    DateTime CreatedAtUtc,
    DateTime DueAtUtc,
    bool IsOverdue);

public sealed record TicketDetailDto(
    int Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    PersonDto CreatedBy,
    PersonDto? AssignedTo,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime DueAtUtc,
    DateTime? ResolvedAtUtc,
    bool IsOverdue,
    IReadOnlyList<TicketStatus> AllowedNextStatuses,
    IReadOnlyList<CommentDto> Comments);

public sealed record CommentDto(int Id, PersonDto Author, string Body, DateTime CreatedAtUtc);

public sealed record TicketStatsDto(int Total, IReadOnlyDictionary<string, int> ByStatus, int Overdue);
