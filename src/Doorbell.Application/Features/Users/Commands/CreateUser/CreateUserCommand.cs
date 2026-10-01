using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Users.Commands.CreateUser
{
    public sealed record CreateUserCommand(
    string Name) : IRequest<Guid>;
}
