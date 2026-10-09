using MediatR;

namespace Doorbell.Application.Features.Users.Commands.CreateUser
{
    public sealed record CreateUserCommand(
        string Name,
        string? SubjectId = null,
        string? Email = null,
        string? Username = null) : IRequest<Guid>;
}