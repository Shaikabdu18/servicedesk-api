using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Repositories;
using ServiceDesk.Api.Security;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IUserRepository users, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>The logged-in user's profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await users.GetByIdAsync(currentUser.Id, ct);
        return user is null ? NotFound() : Ok(user.ToDto());
    }

    /// <summary>Agents and admins who can be assigned tickets.</summary>
    [HttpGet("agents")]
    [Authorize(Roles = "Agent,Admin")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Agents(CancellationToken ct) =>
        Ok((await users.GetAgentsAsync(ct)).Select(u => u.ToDto()).ToList());
}
