using Api.Application.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class HealthEndpointsModule
{
    internal static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<HealthEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Get(cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/ready", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<HealthEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Ready(cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/ready/history", async (HttpContext httpContext) =>
        {
            var handler = ActivatorUtilities.CreateInstance<HealthEndpointsHandler>(httpContext.RequestServices);
            var result = handler.ReadyHistory();
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/health/slo", async (HttpContext httpContext, int maxEndpoints = 20) =>
        {
            var handler = ActivatorUtilities.CreateInstance<HealthEndpointsHandler>(httpContext.RequestServices);
            var result = handler.SloSummary(maxEndpoints);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        return api;
    }
}