using System.Security.Claims;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace Doorbell.Infrastructure.Security
{
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private Guid? _resolvedUserId;
        private bool _resolved;

        public HttpContextCurrentUser(
            IHttpContextAccessor httpContextAccessor,
            IUserRepository userRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        public string? SubjectId
        {
            get
            {
                var principal = _httpContextAccessor.HttpContext?.User;
                return principal?.FindFirst("sub")?.Value
                       ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
        }

        public async Task<Guid?> GetUserIdAsync(
            CancellationToken cancellationToken = default)
        {
            if (_resolved)
            {
                return _resolvedUserId;
            }

            _resolved = true;

            var subjectId = SubjectId;

            if (subjectId is null || !IsAuthenticated)
            {
                _resolvedUserId = null;
                return null;
            }

            var user = await _userRepository.GetBySubjectIdAsync(
                subjectId,
                cancellationToken);

            _resolvedUserId = user?.Id;

            return _resolvedUserId;
        }
    }
}