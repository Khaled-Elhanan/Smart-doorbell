using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;

public sealed record AcknowledgeRingCommand(Guid RingEventId) : IRequest<AcknowledgeRingResult>;
