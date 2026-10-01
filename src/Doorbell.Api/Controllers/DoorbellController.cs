using Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell;
using Doorbell.Application.Features.Doorbell.Commands.ReceiveRing;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Doorbell.Api.Controllers;

[ApiController]
[Route("api/doorbells")]
public sealed class DoorbellController : ControllerBase
{
    private readonly ISender _sender;

    public DoorbellController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("configure")]
    public async Task<IActionResult> Configure(
        ConfigureDoorbellCommand command,
        CancellationToken cancellationToken)
    {
        var compositionId = await _sender.Send(
            command,
            cancellationToken);

        return Ok(new { compositionId });
    }

    [HttpPost("rings")]
    public async Task<IActionResult> ReceiveRing(
        ReceiveRingCommand command,
        CancellationToken cancellationToken)
    {
        var ringEventId = await _sender.Send(
            command,
            cancellationToken);

        return Ok(new { ringEventId });
    }
}