using Api.Application.Ingest;
using Api.Contracts;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Modules;

internal static class IngestionEndpointsModule
{
    internal static RouteGroupBuilder MapIngestionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/ingest", async (HttpContext httpContext, IngestRequest? request, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Post(request, cancellationToken, httpContext.Request.Path);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/upload", async (HttpContext httpContext, IFormFile? file, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Upload(file, cancellationToken, knowledgeBaseId, httpContext.Request.Path);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest")
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery();

        api.MapGet("/ingest/jobs/{jobId:guid}", async (HttpContext httpContext, Guid jobId) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = handler.GetJobStatus(jobId);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/jobs", async (HttpContext httpContext, int limit = 100) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = handler.ListJobs(limit);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/jobs/dead-letter", async (HttpContext httpContext, int limit = 100) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = handler.ListDeadLetterJobs(limit);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/cancel", async (HttpContext httpContext, Guid jobId) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = handler.CancelJob(jobId);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/retry", async (HttpContext httpContext, Guid jobId, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.RetryJob(jobId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/priority", async (HttpContext httpContext, Guid jobId, IngestJobPriorityUpdateRequest request) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestJobsEndpointsHandler>(httpContext.RequestServices);
            var result = handler.UpdateJobPriority(jobId, request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/approvals/request", async (HttpContext httpContext, SopMutationApprovalRequest request) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = handler.RequestSopMutationApproval(request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/approvals/approve", async (HttpContext httpContext, SopMutationApprovalRequest request) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = handler.ApproveSopMutation(request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/approvals", async (HttpContext httpContext, string knowledgeBaseId, string sourceChecksum) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = handler.GetSopMutationApprovalState(knowledgeBaseId, sourceChecksum);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/preview", async (HttpContext httpContext, IFormFile? file, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Preview(file, cancellationToken, httpContext.Request.Path);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest")
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery();

        api.MapDelete("/ingest/reset", async (HttpContext httpContext, string? confirm, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<IngestEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Reset(confirm, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        return api;
    }
}