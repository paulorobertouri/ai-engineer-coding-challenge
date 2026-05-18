using Api.Controllers;
using Api.Contracts;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class IngestionEndpointsModule
{
    internal static RouteGroupBuilder MapIngestionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/ingest", async (HttpContext httpContext, IngestRequest? request, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = await controller.Post(request, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/upload", async (HttpContext httpContext, IFormFile? file, string? knowledgeBaseId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = await controller.Upload(file, cancellationToken, knowledgeBaseId);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest")
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery();

        api.MapGet("/ingest/jobs/{jobId:guid}", async (HttpContext httpContext, Guid jobId) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = controller.GetJobStatus(jobId);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/jobs", async (HttpContext httpContext, int limit = 100) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = controller.ListJobs(limit);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/jobs/dead-letter", async (HttpContext httpContext, int limit = 100) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = controller.ListDeadLetterJobs(limit);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/cancel", async (HttpContext httpContext, Guid jobId) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = controller.CancelJob(jobId);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/retry", async (HttpContext httpContext, Guid jobId, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = await controller.RetryJob(jobId, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/jobs/{jobId:guid}/priority", async (HttpContext httpContext, Guid jobId, IngestJobPriorityUpdateRequest request) =>
        {
            var controller = EndpointExecution.CreateController<IngestJobsController>(httpContext);
            var result = controller.UpdateJobPriority(jobId, request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/approvals/request", async (HttpContext httpContext, SopMutationApprovalRequest request) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = controller.RequestSopMutationApproval(request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/approvals/approve", async (HttpContext httpContext, SopMutationApprovalRequest request) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = controller.ApproveSopMutation(request);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        api.MapGet("/ingest/approvals", async (HttpContext httpContext, string knowledgeBaseId, string sourceChecksum) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = controller.GetSopMutationApprovalState(knowledgeBaseId, sourceChecksum);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest");

        api.MapPost("/ingest/preview", async (HttpContext httpContext, IFormFile? file, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = await controller.Preview(file, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.Operator)
        .RequireRateLimiting("ingest")
        .Accepts<IFormFile>("multipart/form-data")
        .DisableAntiforgery();

        api.MapDelete("/ingest/reset", async (HttpContext httpContext, string? confirm, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<IngestController>(httpContext);
            var result = await controller.Reset(confirm, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.KnowledgeAdmin)
        .RequireRateLimiting("ingest");

        return api;
    }
}