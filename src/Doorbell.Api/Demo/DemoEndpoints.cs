using System.Text.Json;
using System.Text.Json.Serialization;

namespace Doorbell.Api.Demo;

public sealed record DemoStartRequest
{
    public string? Mode { get; init; }
}

public static class DemoEndpoints
{
    private static readonly JsonSerializerOptions StartRequestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEndpointRouteBuilder MapDemoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/demo/status",
            async Task<IResult> (
                string run,
                DemoSimulationService demo,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(run))
                {
                    return Results.BadRequest(
                        new { error = "The 'run' query parameter is required." });
                }

                var status = await demo.GetStatusAsync(
                    run,
                    cancellationToken);

                return Results.Ok(status);
            });

        endpoints.MapPost(
            "/demo/simulate",
            async Task<IResult> (
                HttpRequest httpRequest,
                DemoSimulationService demo,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var request = await ReadStartRequestAsync(
                        httpRequest,
                        cancellationToken);

                    var mode = string.Equals(
                        request?.Mode,
                        "interactive",
                        StringComparison.OrdinalIgnoreCase)
                        ? DemoMode.Interactive
                        : DemoMode.Auto;

                    var runId = await demo.StartRingAsync(
                        mode,
                        cancellationToken);

                    return Results.Ok(new
                    {
                        runId,
                        mode = mode.ToString()
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "Demo simulation failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            });

        endpoints.MapGet(
            "/demo/runs",
            (DemoSimulationService demo) =>
            {
                return Results.Ok(new { runs = demo.GetRecentRuns() });
            });

        endpoints.MapPost(
            "/demo/runs/{runId}/acknowledge",
            async Task<IResult> (
                string runId,
                DemoSimulationService demo,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await demo.AcknowledgeRunAsync(
                        runId,
                        cancellationToken);

                    return Results.Ok(result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            });

        endpoints.MapPost(
            "/demo/runs/{runId}/reject",
            async Task<IResult> (
                string runId,
                DemoSimulationService demo,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var result = await demo.RejectRunAsync(
                        runId,
                        cancellationToken);

                    return Results.Ok(result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
            });

        return endpoints;
    }

    private static async Task<DemoStartRequest?> ReadStartRequestAsync(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (httpRequest.ContentLength is null or 0
            && !httpRequest.Headers.ContainsKey("Transfer-Encoding"))
        {
            return null;
        }

        httpRequest.EnableBuffering();

        using var reader = new StreamReader(
            httpRequest.Body,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DemoStartRequest>(
                body,
                StartRequestJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
