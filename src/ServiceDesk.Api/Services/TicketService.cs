using ServiceDesk.Api.Common;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Repositories;
using ServiceDesk.Api.Security;
using ServiceDesk.Api.Services.Sla;

namespace ServiceDesk.Api.Services;

public interface ITicketService
{
    Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQuery query, CancellationToken ct = default);
    Task<TicketDetailDto> GetAsync(int id, CancellationToken ct = default);
    Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> AssignAsync(int id, AssignTicketRequest request, CancellationToken ct = default);
    Task<TicketDetailDto> ChangeStatusAsync(int id, ChangeStatusRequest request, CancellationToken ct = default);
    Task<CommentDto> AddCommentAsync(int id, AddCommentRequest request, CancellationToken ct = default);
    Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default);
}

public sealed class TicketService(
    ITicketRepository tickets,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ISlaCalculator sla,
    TimeProvider clock,
    ICurrentUser currentUser) : ITicketService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketQuery query, CancellationToken ct = default)
    {
        var now = Now;
        var criteria = new TicketSearchCriteria(
            Status: query.Status,
            Priority: query.Priority,
            CreatedById: currentUser.IsStaff ? null : currentUser.Id, // customers only see their own tickets
            AssignedToId: query.AssignedToMe == true ? currentUser.Id : null,
            Search: query.Search,
            OverdueOnly: query.OverdueOnly == true,
            NowUtc: now,
            Page: query.Page,
            PageSize: query.PageSize);

        var page = await tickets.SearchAsync(criteria, ct);
        return page.Map(t => t.ToSummaryDto(now));
    }

    public async Task<TicketDetailDto> GetAsync(int id, CancellationToken ct = default)
    {
        var ticket = await LoadAccessibleAsync(id, includeComments: true, ct);
        return ticket.ToDetailDto(Now);
    }

    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        var author = await users.GetByIdAsync(currentUser.Id, ct)
                     ?? throw new ForbiddenException("Your account no longer exists.");

        var now = Now;
        var ticket = Ticket.Create(
            request.Title, request.Description, request.Priority,
            author, now, sla.CalculateDueDate(request.Priority, now));

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(ct);

        return ticket.ToDetailDto(now);
    }

    public async Task<TicketDetailDto> AssignAsync(int id, AssignTicketRequest request, CancellationToken ct = default)
    {
        EnsureStaff();

        var ticket = await tickets.GetByIdAsync(id, includeComments: true, ct)
                     ?? throw new NotFoundException("Ticket", id);
        var agent = await users.GetByIdAsync(request.AgentId, ct)
                    ?? throw new NotFoundException("User", request.AgentId);

        ticket.AssignTo(agent, Now);
        await unitOfWork.SaveChangesAsync(ct);

        return ticket.ToDetailDto(Now);
    }

    public async Task<TicketDetailDto> ChangeStatusAsync(int id, ChangeStatusRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAccessibleAsync(id, includeComments: true, ct);

        if (!currentUser.IsStaff)
        {
            // Customers can only confirm (close) or reopen a ticket that support has resolved.
            var allowed = ticket.Status == TicketStatus.Resolved
                          && (request.Status is TicketStatus.Closed or TicketStatus.InProgress);

            if (!allowed)
            {
                throw new ForbiddenException("Customers can only close or reopen their resolved tickets.");
            }
        }

        ticket.ChangeStatus(request.Status, Now);
        await unitOfWork.SaveChangesAsync(ct);

        return ticket.ToDetailDto(Now);
    }

    public async Task<CommentDto> AddCommentAsync(int id, AddCommentRequest request, CancellationToken ct = default)
    {
        var ticket = await LoadAccessibleAsync(id, includeComments: false, ct);
        var author = await users.GetByIdAsync(currentUser.Id, ct)
                     ?? throw new ForbiddenException("Your account no longer exists.");

        var comment = ticket.AddComment(author, request.Body, Now);
        await unitOfWork.SaveChangesAsync(ct);

        return comment.ToDto();
    }

    public async Task<TicketStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        EnsureStaff();

        var stats = await tickets.GetStatsAsync(Now, ct);
        return new TicketStatsDto(
            stats.Total,
            stats.ByStatus.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            stats.Overdue);
    }

    private async Task<Ticket> LoadAccessibleAsync(int id, bool includeComments, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, includeComments, ct);

        // Customers get 404 (not 403) for other people's tickets so ticket IDs are not leaked.
        if (ticket is null || (!currentUser.IsStaff && ticket.CreatedById != currentUser.Id))
        {
            throw new NotFoundException("Ticket", id);
        }

        return ticket;
    }

    private void EnsureStaff()
    {
        if (!currentUser.IsStaff)
        {
            throw new ForbiddenException();
        }
    }
}
