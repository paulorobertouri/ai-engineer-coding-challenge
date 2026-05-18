using Api.Application.Chat;
using Api.Contracts;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Modules;

internal static class ChatEndpointsModule
{
    internal static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/chat", async (HttpContext httpContext, ChatRequest request, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<ChatEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Post(request, cancellationToken, httpContext.Request.Path);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        api.MapPost("/chat/stream", async (HttpContext httpContext, ChatRequest request, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<ChatEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.Stream(request, httpContext.Response, cancellationToken, httpContext.Request.Path);
            await EndpointExecution.ExecuteIActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        api.MapPost("/chat/feedback", async (HttpContext httpContext, ConversationFeedbackRequest request, CancellationToken cancellationToken) =>
        {
            var handler = ActivatorUtilities.CreateInstance<ChatEndpointsHandler>(httpContext.RequestServices);
            var result = await handler.SubmitFeedback(request, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        return api;
    }
}