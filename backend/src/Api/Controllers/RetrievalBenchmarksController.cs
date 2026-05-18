using Api.Contracts;
using Api.Security;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/operators/retrieval-benchmarks")]
[Authorize(Policy = AuthorizationPolicies.Operator)]
public sealed class RetrievalBenchmarksController(IRetrievalBenchmarkService retrievalBenchmarkService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RetrievalBenchmarkDashboardResponse>> Get([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var entries = await retrievalBenchmarkService.ListAsync(limit, cancellationToken);
        return Ok(new RetrievalBenchmarkDashboardResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Entries = entries
        });
    }

    [HttpPost("run")]
    public async Task<ActionResult<RetrievalBenchmarkEntryDto>> Run(CancellationToken cancellationToken = default)
    {
        var entry = await retrievalBenchmarkService.RunAsync(cancellationToken);
        return Ok(entry);
    }
}
