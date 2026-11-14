using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Tests.Integration;

public sealed class TicketsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object NewTicket(TicketPriority priority = TicketPriority.High) => new
    {
        title = "VPN keeps disconnecting",
        description = "The VPN drops every 10 minutes when I work from home.",
        priority
    };

    private static async Task<TicketDetailDto> CreateTicketAsync(HttpClient client, TicketPriority priority = TicketPriority.High)
    {
        var response = await client.PostAsJsonAsync("/api/tickets", NewTicket(priority), TestApi.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<TicketDetailDto>();
    }

    [Fact]
    public async Task Customer_creates_a_ticket_with_an_sla_due_date()
    {
        var (customer, user) = await factory.NewCustomerAsync();

        var ticket = await CreateTicketAsync(customer, TicketPriority.High);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(user.Id, ticket.CreatedBy.Id);
        Assert.Equal(TimeSpan.FromHours(8), ticket.DueAtUtc - ticket.CreatedAtUtc);
        Assert.Contains(TicketStatus.InProgress, ticket.AllowedNextStatuses);
    }

    [Fact]
    public async Task Invalid_ticket_returns_400_with_field_errors()
    {
        var (customer, _) = await factory.NewCustomerAsync();

        var response = await customer.PostAsJsonAsync(
            "/api/tickets", new { title = "Hi", description = "short", priority = "High" }, TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Title", body);
        Assert.Contains("Description", body);
    }

    [Fact]
    public async Task Customer_cannot_see_another_customers_ticket()
    {
        var (owner, _) = await factory.NewCustomerAsync();
        var (stranger, _) = await factory.NewCustomerAsync();
        var ticket = await CreateTicketAsync(owner);

        var direct = await stranger.GetAsync($"/api/tickets/{ticket.Id}");
        var list = await (await stranger.GetAsync("/api/tickets")).ReadAsync<PagedResult<TicketSummaryDto>>();

        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.DoesNotContain(list.Items, t => t.Id == ticket.Id);
    }

    [Fact]
    public async Task Agent_assigns_and_resolves_then_customer_closes()
    {
        var (customer, _) = await factory.NewCustomerAsync();
        var agent = await factory.LoginAsAgentAsync();
        var ticket = await CreateTicketAsync(customer);

        var agents = await (await agent.GetAsync("/api/users/agents")).ReadAsync<List<UserDto>>();
        var agentId = agents.Single(a => a.Email == TestApi.AgentEmail).Id;

        var assigned = await (await agent.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/assign", new { agentId }, TestApi.Json)).ReadAsync<TicketDetailDto>();
        Assert.Equal(agentId, assigned.AssignedTo?.Id);

        await agent.PatchAsJsonAsync($"/api/tickets/{ticket.Id}/status", new { status = "InProgress" }, TestApi.Json);
        var resolved = await (await agent.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status", new { status = "Resolved" }, TestApi.Json)).ReadAsync<TicketDetailDto>();
        Assert.Equal(TicketStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);

        var closedResponse = await customer.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status", new { status = "Closed" }, TestApi.Json);
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        Assert.Equal(TicketStatus.Closed, (await closedResponse.ReadAsync<TicketDetailDto>()).Status);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_400()
    {
        var (customer, _) = await factory.NewCustomerAsync();
        var agent = await factory.LoginAsAgentAsync();
        var ticket = await CreateTicketAsync(customer);

        var response = await agent.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status", new { status = "Resolved" }, TestApi.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_move_an_open_ticket_forward()
    {
        var (customer, _) = await factory.NewCustomerAsync();
        var ticket = await CreateTicketAsync(customer);

        var response = await customer.PatchAsJsonAsync(
            $"/api/tickets/{ticket.Id}/status", new { status = "InProgress" }, TestApi.Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Comments_are_saved_and_returned_with_the_ticket()
    {
        var (customer, _) = await factory.NewCustomerAsync();
        var ticket = await CreateTicketAsync(customer);

        var added = await customer.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments", new { body = "It happens on Wi-Fi only." }, TestApi.Json);
        var detail = await (await customer.GetAsync($"/api/tickets/{ticket.Id}")).ReadAsync<TicketDetailDto>();

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Contains(detail.Comments, c => c.Body == "It happens on Wi-Fi only.");
    }

    [Fact]
    public async Task Customers_are_forbidden_from_stats()
    {
        var (customer, _) = await factory.NewCustomerAsync();

        var response = await customer.GetAsync("/api/tickets/stats");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stats_include_the_seeded_overdue_ticket()
    {
        var agent = await factory.LoginAsAgentAsync();

        var stats = await (await agent.GetAsync("/api/tickets/stats")).ReadAsync<TicketStatsDto>();

        Assert.True(stats.Total >= 3);
        Assert.True(stats.Overdue >= 1);
        Assert.Equal(stats.Total, stats.ByStatus.Values.Sum());
    }

    [Fact]
    public async Task Overdue_filter_returns_only_overdue_tickets()
    {
        var agent = await factory.LoginAsAgentAsync();

        var page = await (await agent.GetAsync("/api/tickets?overdueOnly=true")).ReadAsync<PagedResult<TicketSummaryDto>>();

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, t => Assert.True(t.IsOverdue));
    }
}
