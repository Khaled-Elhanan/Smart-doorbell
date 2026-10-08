using System.Security.Cryptography;
using System.Text;
using Doorbell.Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.Storage;

public sealed class LocalObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly ILogger<LocalObjectStorage> _logger;
    private readonly string _rootPath;
    private readonly Uri _publicBaseUri;

    public LocalObjectStorage(
        IOptions<ObjectStorageOptions> options,
        ILogger<LocalObjectStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        var configuredRoot = string.IsNullOrWhiteSpace(_options.RootPath)
            ? "objectstorage"
            : _options.RootPath;

        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(AppContext.BaseDirectory, configuredRoot));

        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? "http://localhost:5260"
            : _options.PublicBaseUrl;

        _publicBaseUri = new Uri(
            baseUrl.TrimEnd('/') + "/",
            UriKind.Absolute);
    }

    public Task<Uri> CreateUploadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            CreateSignedUrl(storageKey, "w", expiresIn));
    }

    public Task<Uri> CreateDownloadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            CreateSignedUrl(storageKey, "r", expiresIn));
    }

    public bool TryValidateSignature(
        string storageKey,
        string permission,
        string expires,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) ||
            string.IsNullOrWhiteSpace(signature) ||
            !long.TryParse(expires, out var expiresUnix) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
        {
            return false;
        }

        var expected = ComputeSignature(
            storageKey,
            permission,
            expiresUnix);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    public string GetFullPath(string storageKey)
    {
        var relative = storageKey
            .Replace('\\', '/')
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(
            Path.Combine(_rootPath, relative));

        var root = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Storage key '{storageKey}' resolves outside the storage root.");
        }

        return fullPath;
    }

    public async Task WriteAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(storageKey);

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);

        await content.CopyToAsync(file, cancellationToken);

        _logger.LogInformation(
            "Stored object {StorageKey} ({Length} bytes).",
            storageKey,
            file.Length);
    }

    public Stream? OpenRead(string storageKey)
    {
        var fullPath = GetFullPath(storageKey);

        return File.Exists(fullPath)
            ? File.OpenRead(fullPath)
            : null;
    }

    private Uri CreateSignedUrl(
        string storageKey,
        string permission,
        TimeSpan expiresIn)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException(
                "Storage key is required.",
                nameof(storageKey));
        }

        var lifetime = expiresIn > TimeSpan.Zero
            ? expiresIn
            : TimeSpan.FromMinutes(5);

        var expires = DateTimeOffset.UtcNow
            .Add(lifetime)
            .ToUnixTimeSeconds();

        var signature = ComputeSignature(
            storageKey,
            permission,
            expires);

        var url =
            $"{_publicBaseUri}storage/{storageKey.TrimStart('/')}" +
            $"?permission={permission}&expires={expires}&signature={signature}";

        return new Uri(url, UriKind.Absolute);
    }

    private string ComputeSignature(
        string storageKey,
        string permission,
        long expiresUnix)
    {
        var payload = $"{storageKey}\n{permission}\n{expiresUnix}";

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(_options.SigningKey));

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(payload));

        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
