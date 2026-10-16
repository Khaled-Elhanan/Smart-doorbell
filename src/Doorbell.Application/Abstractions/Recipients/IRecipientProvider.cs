using System;
using System.Threading;
using System.Threading.Tasks;

namespace Doorbell.Application.Abstractions.Recipients
{
    public interface IRecipientProvider
    {
        Task<Guid> GetRecipientUserIdAsync(
            CancellationToken cancellationToken = default);
    }
}
