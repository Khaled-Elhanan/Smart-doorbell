using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Exceptions;
using MediatR;

namespace Doorbell.Application.Features.Users.Commands.RegisterDeviceToken;

public sealed class RegisterDeviceTokenCommandHandler
    : IRequestHandler<RegisterDeviceTokenCommand, Guid>
{
    private readonly IUserDeviceTokenRepository _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public RegisterDeviceTokenCommandHandler(
        IUserDeviceTokenRepository tokenRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _tokenRepository = tokenRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        RegisterDeviceTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException(
                "Authentication is required to register a device token.");
        }

        var userId = await _currentUser.GetUserIdAsync(cancellationToken);

        if (userId is null)
        {
            throw new UnauthorizedException(
                "The authenticated identity is not associated with a known user.");
        }

        var token = UserDeviceToken.Create(
            userId.Value,
            request.Token,
            request.Platform);

        await _tokenRepository.AddAsync(token, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return token.Id;
    }
}
