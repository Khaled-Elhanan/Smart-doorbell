namespace Doorbell.Application.Abstractions.Security
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }

        string? SubjectId { get; }

        Task<Guid?> GetUserIdAsync(
            CancellationToken cancellationToken = default);
    }
}