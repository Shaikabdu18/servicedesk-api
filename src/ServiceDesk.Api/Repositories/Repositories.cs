using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Repositories;

/// <summary>Unit of Work: services change entities through repositories, then commit once.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAgentsAsync(CancellationToken ct = default);
    void Add(User user);
}

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public async Task<IReadOnlyList<User>> GetAgentsAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == Role.Agent || u.Role == Role.Admin)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);
}

public sealed record TicketSearchCriteria(
    TicketStatus? Status,
    TicketPriority? Priority,
    int? CreatedById,
    int? AssignedToId,
    string? Search,
    bool OverdueOnly,
    DateTime NowUtc,
    int Page,
    int PageSize);

public sealed record TicketStats(int Total, IReadOnlyDictionary<TicketStatus, int> ByStatus, int Overdue);

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(int id, bool includeComments, CancellationToken ct = default);
    Task<PagedResult<Ticket>> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct = default);
    Task<TicketStats> GetStatsAsync(DateTime nowUtc, CancellationToken ct = default);
    void Add(Ticket ticket);
}

public sealed class TicketRepository(AppDbContext db) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(int id, bool includeComments, CancellationToken ct = default)
    {
        IQueryable<Ticket> query = db.Tickets
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo);

        if (includeComments)
        {
            query = query.Include(t => t.Comments.OrderBy(c => c.CreatedAtUtc)).ThenInclude(c => c.Author);
        }

        return query.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PagedResult<Ticket>> SearchAsync(TicketSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = db.Tickets.AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (criteria.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (criteria.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (criteria.CreatedById is { } creatorId)
        {
            query = query.Where(t => t.CreatedById == creatorId);
        }

        if (criteria.AssignedToId is { } assigneeId)
        {
            query = query.Where(t => t.AssignedToId == assigneeId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search.Trim();
            query = query.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        if (criteria.OverdueOnly)
        {
            var now = criteria.NowUtc;
            query = query.Where(t =>
                (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) && t.DueAtUtc < now);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueAtUtc)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Ticket>(items, criteria.Page, criteria.PageSize, total);
    }

    public async Task<TicketStats> GetStatsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // One grouped query for the status breakdown instead of one query per status.
        var counts = await db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var overdue = await db.Tickets.CountAsync(t =>
            (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) && t.DueAtUtc < nowUtc, ct);

        var byStatus = Enum.GetValues<TicketStatus>()
            .ToDictionary(s => s, s => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0);

        return new TicketStats(byStatus.Values.Sum(), byStatus, overdue);
    }

    public void Add(Ticket ticket) => db.Tickets.Add(ticket);
}
