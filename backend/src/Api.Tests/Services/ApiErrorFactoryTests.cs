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
}
