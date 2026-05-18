using Api.Controllers;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class SourcesEndpointsModule
{
    internal static RouteGroupBuilder MapSourcesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/sources", async (HttpContext httpContext, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.List(knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapDelete("/sources", async (HttpContext httpContext, string source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.Delete(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin);

        api.MapGet("/sources/document", async (HttpContext httpContext, string source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.GetDocument(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/update-alert", async (HttpContext httpContext, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.GetUpdateAlert(knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/compare", async (HttpContext httpContext, string? source, string? knowledgeBaseId, string? citationChunkId, bool includeUnchanged, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.GetComparison(source, knowledgeBaseId, citationChunkId, includeUnchanged, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        api.MapGet("/sources/quality", async (HttpContext httpContext, string? source, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<SourcesController>(httpContext);
            var result = await controller.GetQuality(source, knowledgeBaseId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser);

        return api;
    }
}