namespace Doorbell.Api.Demo;

public static class DemoEndpoints
{
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
                DemoSimulationService demo,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var runId = await demo.StartRingAsync(
                        cancellationToken);

                    return Results.Ok(new { runId });
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        title: "Demo simulation failed",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError);
                }
            });

        return endpoints;
    }
}