using Api.Contracts;
using Api.Application.Operators;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Api.Tests.Controllers;

public sealed class RetrievalBenchmarksControllerTests
{
    [Fact]
    public async Task Get_ReturnsDashboardEntries()
    {
        var service = new Mock<IRetrievalBenchmarkService>();
        service
            .Setup(dependency => dependency.ListAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RetrievalBenchmarkEntryDto
                {
                    RunId = Guid.NewGuid(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Commit = "local",
                    FixtureCount = 3,
                    Precision = 0.8,
                    Recall = 0.7
                }
            ]);

        var handler = new RetrievalBenchmarksEndpointsHandler(service.Object);
        var result = await handler.Get(20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<RetrievalBenchmarkDashboardResponse>(ok.Value);
        Assert.Single(payload.Entries);
    }

    [Fact]
    public async Task Run_ReturnsCreatedBenchmarkEntry()
    {
        var expected = new RetrievalBenchmarkEntryDto
        {
            RunId = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Commit = "local",
            FixtureCount = 3,
            Precision = 0.75,
            Recall = 0.66
        };

        var service = new Mock<IRetrievalBenchmarkService>();
        service
            .Setup(dependency => dependency.RunAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new RetrievalBenchmarksEndpointsHandler(service.Object);
        var result = await handler.Run(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<RetrievalBenchmarkEntryDto>(ok.Value);
        Assert.Equal(expected.RunId, payload.RunId);
    }
}
