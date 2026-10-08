using Doorbell.Application.Abstractions.Repositories;
using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.ReconcileChimeTimeouts;

public sealed record TimeOutChimeCommand(
    ChimeCommandTimeout Candidate) : IRequest;