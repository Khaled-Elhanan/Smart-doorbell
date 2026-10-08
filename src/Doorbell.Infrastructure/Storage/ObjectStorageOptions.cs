namespace Doorbell.Infrastructure.Storage;

public sealed class ObjectStorageOptions
{
    public string RootPath { get; set; } = "objectstorage";

    public string PublicBaseUrl { get; set; } = "http://localhost:5260";

    public string SigningKey { get; set; } = "local-object-storage-dev-key";
}
