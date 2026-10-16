using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Recipients;
using Doorbell.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Doorbell.Infrastructure.Persistence
{
    public sealed class RecipientProvider : IRecipientProvider
    {
        private const string RecipientSubjectId = "doorbell-recipient";
        private const string RecipientName = "Doorbell Recipient";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private Guid? _recipientUserId;

        public RecipientProvider(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<Guid> GetRecipientUserIdAsync(
            CancellationToken cancellationToken = default)
        {
            var cached = _recipientUserId;

            if (cached is not null)
            {
                return cached.Value;
            }

            await _lock.WaitAsync(cancellationToken);

            try
            {
                var resolved = _recipientUserId;

                if (resolved is not null)
                {
                    return resolved.Value;
                }

                using var scope = _scopeFactory.CreateScope();

                var repository = scope.ServiceProvider
                    .GetRequiredService<IUserRepository>();

                var unitOfWork = scope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

                var recipient = await repository.GetBySubjectIdAsync(
                    RecipientSubjectId,
                    cancellationToken);

                if (recipient is null)
                {
                    recipient = new User(RecipientName, RecipientSubjectId);

                    await repository.AddAsync(recipient, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                _recipientUserId = recipient.Id;

                return recipient.Id;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
