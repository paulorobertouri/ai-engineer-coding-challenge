using Api.Controllers;
using Api.Contracts;
using Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Api.Modules;

internal static class ChatEndpointsModule
{
    internal static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/chat", async (HttpContext httpContext, ChatRequest request, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<ChatController>(httpContext);
            var result = await controller.Post(request, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        api.MapPost("/chat/stream", async (HttpContext httpContext, ChatRequest request, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<ChatController>(httpContext);
            var result = await controller.Stream(request, cancellationToken);
            await EndpointExecution.ExecuteIActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        api.MapPost("/chat/feedback", async (HttpContext httpContext, ConversationFeedbackRequest request, CancellationToken cancellationToken) =>
        {
            var controller = EndpointExecution.CreateController<ChatController>(httpContext);
            var result = await controller.SubmitFeedback(request, cancellationToken);
            await EndpointExecution.ExecuteActionResultAsync(httpContext, result);
        })
        .RequireAuthorization(AuthorizationPolicies.ChatUser)
        .RequireRateLimiting("chat");

        return api;
    }
}