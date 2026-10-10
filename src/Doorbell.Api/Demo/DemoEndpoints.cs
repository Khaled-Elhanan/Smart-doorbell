using Doorbell.Api.Authentication;
using Microsoft.Extensions.DependencyInjection;

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

        endpoints.MapPost(
            "/demo/token",
            async Task<IResult> (
                DemoSimulationService demo,
                IServiceProvider services,
                CancellationToken cancellationToken) =>
            {
                var issuer = services.GetService<DevelopmentTokenIssuer>();

                if (issuer is null)
                {
                    return Results.NotFound(new
                    {
                        error = "Development authentication is not enabled."
                    });
                }

                var identity = await demo.GetIdentityAsync(cancellationToken);

                var (token, expiresAtUtc) = issuer.Issue(
                    identity.SubjectId,
                    identity.Name);

                return Results.Ok(new
                {
                    accessToken = token,
                    tokenType = "Bearer",
                    expiresAtUtc,
                    subject = identity.SubjectId,
                    userId = identity.UserId,
                    deviceId = identity.DeviceId
                });
            });

        return endpoints;
    }
}
