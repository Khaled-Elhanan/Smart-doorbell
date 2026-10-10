using Doorbell.Domain.Exceptions;

namespace Doorbell.Domain.Entities
{
    public sealed class UserDeviceToken
    {
        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public string Token { get; private set; } = null!;

        public string Platform { get; private set; } = null!;

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime? LastSeenAtUtc { get; private set; }

        private UserDeviceToken()
        {
        }

        private UserDeviceToken(
            Guid userId,
            string token,
            string platform)
        {
            if (userId == Guid.Empty)
                throw new DomainException("UserId cannot be empty.");

            if (string.IsNullOrWhiteSpace(token))
                throw new DomainException("Device token is required.");

            if (string.IsNullOrWhiteSpace(platform))
                throw new DomainException("Device platform is required.");

            Id = Guid.NewGuid();
            UserId = userId;
            Token = token.Trim();
            Platform = platform.Trim();
            CreatedAtUtc = DateTime.UtcNow;
        }

        public static UserDeviceToken Create(
            Guid userId,
            string token,
            string platform)
        {
            return new UserDeviceToken(userId, token, platform);
        }

        public void Touch()
        {
            LastSeenAtUtc = DateTime.UtcNow;
        }
    }
}
