using MediatR;

namespace Doorbell.Application.Features.Users.Commands.RegisterDeviceToken;

public sealed record RegisterDeviceTokenCommand(
    string Token,
    string Platform) : IRequest<Guid>;
