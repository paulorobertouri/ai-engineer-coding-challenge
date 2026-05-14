using Api.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests;

public class ApiErrorFactoryTests
{
    [Fact]
    public void RateLimit_ReturnsProblemDetailsWithTypedCode()
    {
        var details = ApiErrorFactory.RateLimit("/api/v1/chat");

        Assert.Equal(StatusCodes.Status429TooManyRequests, details.Status);
        Assert.Equal(ApiErrorFactory.RateLimitErrorCode, details.Extensions["code"]);
    }

    [Fact]
    public void RequestTimeout_ReturnsProblemDetailsWithTypedCode()
    {
        var details = ApiErrorFactory.RequestTimeout("Request timed out.", "The request timed out.");

        Assert.Equal(StatusCodes.Status408RequestTimeout, details.Status);
        Assert.Equal(ApiErrorFactory.RequestTimeoutErrorCode, details.Extensions["code"]);
    }
}
