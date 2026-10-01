using Doorbell.Application.Features.Devices.Commands.CreateDevice;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Doorbell.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly ISender _sender;

    public DevicesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateDeviceCommand command,
        CancellationToken cancellationToken)
    {
        var deviceId = await _sender.Send(
            command,
            cancellationToken);

        return Ok(new { deviceId });
    }
}