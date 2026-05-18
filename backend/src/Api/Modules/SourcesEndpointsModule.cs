using Api.Application.Sources;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Modules;

internal static class SourcesEndpointsModule
{
    internal static RouteGroupBuilder MapSourcesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sources", async (HttpContext httpContext, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.List(knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapDelete("/sources", async (HttpContext httpContext, string source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Delete(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin);

        api.MapGet("/sources/document", async (HttpContext httpContext, string source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.GetDocument(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/update-alert", async (HttpContext httpContext, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.GetUpdateAlert(knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/compare", async (HttpContext httpContext, string? source, string? knowledgeBaseId, string? citationChunkId, bool includeUnchanged, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.GetComparison(source, knowledgeBaseId, citationChunkId, includeUnchanged, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/quality", async (HttpContext httpContext, string? source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<SourcesEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.GetQuality(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        return api;
    }
}