using Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class HealthEndpointsModule
{
    internal static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/health", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<HealthController>(httpContext);
            var result = await controller.Get(cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/ready", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<HealthController>(httpContext);
            var result = await controller.Ready(cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/ready/history", async (HttpContext httpContext) =>
        {
            var controller = EndpointExecution.CreateController<HealthController>(httpContext);
            var result = controller.ReadyHistory();
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        api.MapGet("/health/slo", async (HttpContext httpContext, int maxEndpoints = 20) =>
        {
            var controller = EndpointExecution.CreateController<HealthController>(httpContext);
            var result = controller.SloSummary(maxEndpoints);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        });

        return api;
    }
}