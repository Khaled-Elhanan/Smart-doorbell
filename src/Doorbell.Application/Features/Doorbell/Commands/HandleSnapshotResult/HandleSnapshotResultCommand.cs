using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.HandleSnapshotResult;

public sealed record HandleSnapshotResultCommand(
    string RequestId,
    string Resolution,
    DateTime CapturedAtUtc) : IRequest;
