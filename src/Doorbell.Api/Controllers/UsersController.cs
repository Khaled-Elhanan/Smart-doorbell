using Doorbell.Application.Features.Users.Commands.CreateUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Doorbell.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await _sender.Send(
            command,
            cancellationToken);

        return Ok(new { userId });
    }
}