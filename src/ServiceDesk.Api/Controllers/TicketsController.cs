using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Common;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    private const string Staff = "Agent,Admin";

    /// <summary>Search tickets. Customers see only their own; agents and admins see all.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TicketSummaryDto>>> Search([FromQuery] TicketQuery query, CancellationToken ct) =>
        Ok(await tickets.SearchAsync(query, ct));

    /// <summary>Get a ticket with its comments and the statuses it can move to next.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> Get(int id, CancellationToken ct) =>
        Ok(await tickets.GetAsync(id, ct));

    /// <summary>Raise a new ticket. The SLA due date is set from its priority.</summary>
    [HttpPost]
    [ProducesResponseType<TicketDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetailDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var created = await tickets.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Assign a ticket to an agent or admin.</summary>
    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = Staff)]
    [ProducesResponseType<TicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketDetailDto>> Assign(int id, AssignTicketRequest request, CancellationToken ct) =>
        Ok(await tickets.AssignAsync(id, request, ct));

    /// <summary>Move a ticket through its workflow (Open, InProgress, Resolved, Closed).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<TicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketDetailDto>> ChangeStatus(int id, ChangeStatusRequest request, CancellationToken ct) =>
        Ok(await tickets.ChangeStatusAsync(id, request, ct));

    /// <summary>Add a comment to a ticket.</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> AddComment(int id, AddCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tickets.AddCommentAsync(id, request, ct));

    /// <summary>Ticket counts by status and the number of overdue tickets.</summary>
    [HttpGet("stats")]
    [Authorize(Roles = Staff)]
    [ProducesResponseType<TicketStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketStatsDto>> Stats(CancellationToken ct) =>
        Ok(await tickets.GetStatsAsync(ct));
}
