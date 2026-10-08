using Doorbell.Application.Abstractions.Repositories;
using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.ReconcileSnapshotTimeouts;

public sealed record TimeOutSnapshotCommand(
    SnapshotCommandTimeout Candidate) : IRequest;
