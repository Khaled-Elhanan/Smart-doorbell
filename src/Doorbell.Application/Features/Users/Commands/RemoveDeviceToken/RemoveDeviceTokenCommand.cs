using MediatR;

namespace Doorbell.Application.Features.Users.Commands.RemoveDeviceToken;

public sealed record RemoveDeviceTokenCommand(string Token) : IRequest<bool>;
