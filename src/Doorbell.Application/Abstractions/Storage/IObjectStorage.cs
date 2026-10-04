namespace Doorbell.Application.Abstractions.Storage;
// Deal with object storage -> Azure 
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