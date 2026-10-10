namespace Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;

public sealed record AcknowledgeRingResult(
    Guid RingEventId,
    bool WasAcknowledged,
    bool AlreadyAcknowledged,
    DateTime? AcknowledgedAtUtc);
