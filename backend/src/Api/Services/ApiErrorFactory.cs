using Microsoft.AspNetCore.Mvc;

namespace Api.Services;

public static class ApiErrorFactory
{
    public const string ValidationErrorCode = "validation_error";
    public const string ConflictErrorCode = "conflict";
    public const string BadRequestErrorCode = "bad_request";
    public const string NotFoundErrorCode = "not_found";
    public const string RateLimitErrorCode = "rate_limit_exceeded";
    public const string RequestTimeoutErrorCode = "request_timeout";
    public const string InternalServerErrorCode = "internal_server_error";

    public static ValidationProblemDetails Validation(string field, string message, string title)
    {
        var details = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [message]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title
        };

        details.Extensions["code"] = ValidationErrorCode;
        return details;
    }

    public static ProblemDetails Conflict(string title, string detail)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title,
            Detail = detail
        };

        details.Extensions["code"] = ConflictErrorCode;
        return details;
    }

    public static ProblemDetails BadRequest(string title, string detail)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail
        };

        details.Extensions["code"] = BadRequestErrorCode;
        return details;
    }

    public static ProblemDetails NotFound(string title, string detail)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = title,
            Detail = detail
        };

        details.Extensions["code"] = NotFoundErrorCode;
        return details;
    }

    public static ProblemDetails RateLimit(string path)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Rate limit exceeded.",
            Detail = "Too many requests were sent in a short period. Please retry later.",
            Instance = path
        };

        details.Extensions["code"] = RateLimitErrorCode;
        return details;
    }

    public static ProblemDetails RequestTimeout(string title, string detail, string? instance = null)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status408RequestTimeout,
            Title = title,
            Detail = detail,
            Instance = instance
        };

        details.Extensions["code"] = RequestTimeoutErrorCode;
        return details;
    }

    public static ProblemDetails InternalServerError(string detail, string instance)
    {
        var details = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Detail = detail,
            Instance = instance
        };

        details.Extensions["code"] = InternalServerErrorCode;
        return details;
    }
}
