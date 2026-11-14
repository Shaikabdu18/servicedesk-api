using ServiceDesk.Api.Common;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Sla;

namespace ServiceDesk.Tests.Unit;

public class TicketWorkflowTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    public void CanTransition_follows_the_ticket_lifecycle(TicketStatus from, TicketStatus to, bool expected) =>
        Assert.Equal(expected, TicketWorkflow.CanTransition(from, to));
}

public class SlaCalculatorTests
{
    private static readonly SlaCalculator Calculator = new(
    [
        new CriticalSlaPolicy(), new HighSlaPolicy(), new MediumSlaPolicy(), new LowSlaPolicy()
    ]);

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void Due_date_is_created_time_plus_priority_target(TicketPriority priority, int hours)
    {
        var created = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(created.AddHours(hours), Calculator.CalculateDueDate(priority, created));
    }

    [Fact]
    public void Missing_policy_throws()
    {
        var calculator = new SlaCalculator([new HighSlaPolicy()]);

        Assert.Throws<InvalidOperationException>(() => calculator.CalculateDueDate(TicketPriority.Low, DateTime.UtcNow));
    }
}

public class TicketTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    private static User NewUser(Role role, int id) =>
        new() { Id = id, FullName = $"{role} {id}", Email = $"{role}{id}@test.dev".ToLowerInvariant(), Role = role };

    private static Ticket NewTicket() =>
        Ticket.Create("Printer not working", "The office printer shows a paper jam error.", TicketPriority.High,
            NewUser(Role.Customer, 1), Now, Now.AddHours(8));

    [Fact]
    public void Assigning_to_a_customer_is_rejected()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainRuleException>(() => ticket.AssignTo(NewUser(Role.Customer, 2), Now));
    }

    [Fact]
    public void Resolving_sets_resolved_time_and_reopening_clears_it()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.InProgress, Now);

        ticket.ChangeStatus(TicketStatus.Resolved, Now.AddHours(2));
        Assert.Equal(Now.AddHours(2), ticket.ResolvedAtUtc);

        ticket.ChangeStatus(TicketStatus.InProgress, Now.AddHours(3));
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        var ticket = NewTicket();

        var error = Assert.Throws<DomainRuleException>(() => ticket.ChangeStatus(TicketStatus.Resolved, Now));
        Assert.Contains("Open", error.Message);
    }

    [Fact]
    public void Closed_ticket_rejects_comments()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Closed, Now);

        Assert.Throws<DomainRuleException>(() => ticket.AddComment(NewUser(Role.Agent, 3), "Any update?", Now));
    }

    [Fact]
    public void Ticket_is_overdue_only_while_active_and_past_due()
    {
        var ticket = NewTicket();

        Assert.False(ticket.IsOverdue(Now.AddHours(7)));
        Assert.True(ticket.IsOverdue(Now.AddHours(9)));

        ticket.ChangeStatus(TicketStatus.Closed, Now.AddHours(9));
        Assert.False(ticket.IsOverdue(Now.AddHours(10)));
    }
}
