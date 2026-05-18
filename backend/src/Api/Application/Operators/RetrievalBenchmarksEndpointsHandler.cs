using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Application.Operators;

public sealed class RetrievalBenchmarksEndpointsHandler(IRetrievalBenchmarkService retrievalBenchmarkService)
{
    public async Task<ActionResult<RetrievalBenchmarkDashboardResponse>> Get(int limit = 20, CancellationToken cancellationToken = default)
    {
        var entries = await retrievalBenchmarkService.ListAsync(limit, cancellationToken);
        return new OkObjectResult(new RetrievalBenchmarkDashboardResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Entries = entries
        });
    }

    public async Task<ActionResult<RetrievalBenchmarkEntryDto>> Run(CancellationToken cancellationToken = default)
    {
        var entry = await retrievalBenchmarkService.RunAsync(cancellationToken);
        return new OkObjectResult(entry);
    }
}