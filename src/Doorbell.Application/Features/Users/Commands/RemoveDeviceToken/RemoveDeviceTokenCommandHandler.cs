using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Domain.Exceptions;
using MediatR;

namespace Doorbell.Application.Features.Users.Commands.RemoveDeviceToken;

public sealed class RemoveDeviceTokenCommandHandler
    : IRequestHandler<RemoveDeviceTokenCommand, bool>
{
    private readonly IUserDeviceTokenRepository _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public RemoveDeviceTokenCommandHandler(
        IUserDeviceTokenRepository tokenRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _tokenRepository = tokenRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(
        RemoveDeviceTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException(
                "Authentication is required to remove a device token.");
        }

        var userId = await _currentUser.GetUserIdAsync(cancellationToken);

        if (userId is null)
        {
            throw new UnauthorizedException(
                "The authenticated identity is not associated with a known user.");
        }

        var removed = await _tokenRepository.RemoveAsync(
            userId.Value,
            request.Token,
            cancellationToken);

        if (removed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return removed;
    }
}
