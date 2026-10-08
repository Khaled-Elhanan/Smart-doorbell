using Doorbell.Infrastructure.Storage;

namespace Doorbell.Api.Endpoints;

public static class ObjectStorageEndpoints
{
    public static IEndpointRouteBuilder MapObjectStorageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
            "/storage/{**storageKey}",
            async Task<IResult> (
                string storageKey,
                HttpRequest request,
                LocalObjectStorage storage,
                CancellationToken cancellationToken) =>
            {
                if (!IsAuthorized(storage, storageKey, request, "w"))
                {
                    return Results.Unauthorized();
                }

                await storage.WriteAsync(
                    storageKey,
                    request.Body,
                    cancellationToken);

                return Results.Ok();
            });

        endpoints.MapGet(
            "/storage/{**storageKey}",
            IResult (
                string storageKey,
                HttpRequest request,
                LocalObjectStorage storage) =>
            {
                if (!IsAuthorized(storage, storageKey, request, "r"))
                {
                    return Results.Unauthorized();
                }

                var stream = storage.OpenRead(storageKey);

                return stream is null
                    ? Results.NotFound()
                    : Results.Stream(
                        stream,
                        "application/octet-stream");
            });

        return endpoints;
    }

    private static bool IsAuthorized(
        LocalObjectStorage storage,
        string storageKey,
        HttpRequest request,
        string permission)
    {
        return string.Equals(
                   request.Query["permission"].ToString(),
                   permission,
                   StringComparison.Ordinal) &&
               storage.TryValidateSignature(
                   storageKey,
                   permission,
                   request.Query["expires"].ToString(),
                   request.Query["signature"].ToString());
    }
}
