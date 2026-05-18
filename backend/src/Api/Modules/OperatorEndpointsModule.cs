using Api.Application.Operators;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class OperatorEndpointsModule
{
    internal static RouteGroupBuilder MapOperatorEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/operators/audit", async (HttpContext httpContext, string? knowledgeBaseId, string? feedbackType, int lookbackHours = 168, CancellationToken cancellationToken = default) =>
        {
            var handler = ActivatorUtilities.CreateInstance<OperatorAuditEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.GetDashboard(knowledgeBaseId, feedbackType, lookbackHours, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator);

        api.MapGet("/operators/retrieval-benchmarks", async (HttpContext httpContext, int limit = 20, CancellationToken cancellationToken = default) =>
        {
            var handler = ActivatorUtilities.CreateInstance<RetrievalBenchmarksEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Get(limit, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator);

        api.MapPost("/operators/retrieval-benchmarks/run", async (HttpContext httpContext, CancellationToken cancellationToken = default) =>
        {
            var handler = ActivatorUtilities.CreateInstance<RetrievalBenchmarksEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Run(cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator);

        return api;
    }
}