using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;

namespace Api.Modules;

internal static class EndpointExecution
{
    internal static TController CreateController<TController>(HttpContext httpContext)
        where TController : ControllerBase
    {
        var controller = ActivatorUtilities.CreateInstance<TController>(httpContext.RequestServices);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = httpContext.GetRouteData(),
        };
        return controller;
    }

    internal static async Task ExecuteActionResultAsync<T>(HttpContext httpContext, ActionResult<T> actionResult)
    {
        var resolved = actionResult.Result ?? new OkObjectResult(actionResult.Value);
        await ExecuteIActionResultAsync(httpContext, resolved);
    }

    internal static Task ExecuteIActionResultAsync(HttpContext httpContext, IActionResult actionResult)
    {
        var actionContext = new ActionContext(
            httpContext,
            httpContext.GetRouteData(),
            new ActionDescriptor());

        return actionResult.ExecuteResultAsync(actionContext);
    }
}