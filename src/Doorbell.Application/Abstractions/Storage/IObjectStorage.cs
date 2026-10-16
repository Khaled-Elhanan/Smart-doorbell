namespace Doorbell.Application.Abstractions.Storage;
public interface IObjectStorage
{
    Task<Uri> CreateUploadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken);

    Task<Uri> CreateDownloadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken);
}