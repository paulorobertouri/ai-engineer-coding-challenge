using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Application.Ingest;

public sealed class ResetKnowledgeBaseHandler(
    IWebHostEnvironment environment,
    IVectorStoreService vectorStoreService,
    IOptions<ChallengeOptions> challengeOptions,
    ILogger logger)
{
    public const string RequiredConfirmation = "RESET";

    public async Task<ActionResult<object>> HandleAsync(ResetKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return new NotFoundObjectResult(ApiErrorFactory.NotFound(
                "Reset endpoint unavailable.",
                "Reset endpoint is only available in Development."));
        }

        if (!string.Equals(command.Confirmation, RequiredConfirmation, StringComparison.Ordinal))
        {
            return new BadRequestObjectResult(ApiErrorFactory.BadRequest(
                "Reset confirmation required.",
                $"Reset requires explicit confirmation. Call with '?confirm={RequiredConfirmation}'."));
        }

        var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
        await vectorStoreService.SaveAsync([], cancellationToken);

        logger.LogWarning("[INGEST] Knowledge base reset executed. Removed {Count} records.", existingRecords.Count);

        return new OkObjectResult((object)new
        {
            accepted = true,
            message = "Knowledge base reset completed. Reingestion is now allowed.",
            deletedRecords = existingRecords.Count,
            vectorStorePath = challengeOptions.Value.VectorStorePath
        });
    }
}
