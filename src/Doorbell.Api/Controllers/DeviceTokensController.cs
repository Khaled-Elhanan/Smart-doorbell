using Doorbell.Application.Features.Users.Commands.RegisterDeviceToken;
using Doorbell.Application.Features.Users.Commands.RemoveDeviceToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Doorbell.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/device-tokens")]
public sealed class DeviceTokensController : ControllerBase
{
    private readonly ISender _sender;

    public DeviceTokensController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var deviceTokenId = await _sender.Send(
            new RegisterDeviceTokenCommand(request.Token, request.Platform),
            cancellationToken);

        return Ok(new { deviceTokenId });
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var removed = await _sender.Send(
            new RemoveDeviceTokenCommand(token),
            cancellationToken);

        return removed ? NoContent() : NotFound();
    }
}

public sealed record RegisterDeviceTokenRequest(string Token, string Platform);
