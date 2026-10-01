using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using MediatR;

namespace Doorbell.Application.Features.Users.Commands.CreateUser
{
    public sealed class CreateUserCommandHandler
    : IRequestHandler<CreateUserCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateUserCommandHandler(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(
            CreateUserCommand request,
            CancellationToken cancellationToken)
        {
            var user = new User(request.Name);

            await _userRepository.AddAsync(
                user,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return user.Id;
        }
    }
}
