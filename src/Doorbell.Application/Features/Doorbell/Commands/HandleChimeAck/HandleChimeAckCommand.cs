using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.HandleChimeAck;

public sealed record HandleChimeAckCommand(string RequestId) : IRequest;