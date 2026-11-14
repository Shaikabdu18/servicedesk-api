using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Sla;

namespace ServiceDesk.Api.Data;

/// <summary>Demo data so the API can be explored in Swagger right after startup.</summary>
public static class DbSeeder
{
    public static readonly IReadOnlyList<(string Name, string Email, string Password, Role Role)> DemoUsers =
    [
        ("Asha Admin", "admin@servicedesk.dev", "Admin@12345", Role.Admin),
        ("Arjun Agent", "agent@servicedesk.dev", "Agent@12345", Role.Agent),
        ("Priya Agent", "agent2@servicedesk.dev", "Agent@12345", Role.Agent),
        ("Chris Customer", "customer@servicedesk.dev", "Customer@12345", Role.Customer)
    ];

    public static async Task SeedAsync(
        AppDbContext db, IPasswordHasher<User> hasher, ISlaCalculator sla,
        TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        var users = DemoUsers.Select(d =>
        {
            var user = new User { FullName = d.Name, Email = d.Email, Role = d.Role, CreatedAtUtc = now };
            user.PasswordHash = hasher.HashPassword(user, d.Password);
            return user;
        }).ToList();

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        var customer = users.Single(u => u.Role == Role.Customer);
        var agent = users.First(u => u.Role == Role.Agent);

        var loginIssueAt = now.AddHours(-3);
        var loginIssue = Ticket.Create(
            "Cannot log in to the billing portal",
            "Since this morning the billing portal rejects my password, even after a reset.",
            TicketPriority.High, customer, loginIssueAt,
            sla.CalculateDueDate(TicketPriority.High, loginIssueAt));
        loginIssue.AddComment(customer, "Tried in Chrome and Edge, same error.", now.AddHours(-2));

        // Created two days ago with a 24h SLA and still in progress, so it shows up as overdue.
        var taxIssueAt = now.AddDays(-2);
        var taxIssue = Ticket.Create(
            "Invoice PDF shows the wrong tax amount",
            "The October invoice PDF shows 12% GST, but our contract rate is 18%.",
            TicketPriority.Medium, customer, taxIssueAt,
            sla.CalculateDueDate(TicketPriority.Medium, taxIssueAt));
        taxIssue.AssignTo(agent, taxIssueAt.AddHours(1));
        taxIssue.ChangeStatus(TicketStatus.InProgress, taxIssueAt.AddHours(1));

        var exportRequestAt = now.AddDays(-5);
        var exportRequest = Ticket.Create(
            "Export monthly reports to Excel",
            "Please add an option to export the monthly usage report as an Excel file.",
            TicketPriority.Low, customer, exportRequestAt,
            sla.CalculateDueDate(TicketPriority.Low, exportRequestAt));
        exportRequest.AssignTo(agent, exportRequestAt.AddHours(2));
        exportRequest.ChangeStatus(TicketStatus.InProgress, exportRequestAt.AddHours(2));
        exportRequest.ChangeStatus(TicketStatus.Resolved, now.AddDays(-1));

        db.Tickets.AddRange(loginIssue, taxIssue, exportRequest);
        await db.SaveChangesAsync(ct);
    }
}
