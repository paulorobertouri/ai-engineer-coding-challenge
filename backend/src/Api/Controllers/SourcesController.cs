using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Security;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthorizationPolicies.ChatUser)]
public sealed class SourcesController(
    ISourceDocumentViewerService sourceDocumentViewerService,
    IVectorStoreService vectorStoreService,
    IChunkingService chunkingService,
    IDocumentExtractionService documentExtractionService,
    IOptions<ChallengeOptions> challengeOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SourceListItemDto>>> List(
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var records = await vectorStoreService.LoadAsync(cancellationToken);

        var sources = records
            .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId))
            .GroupBy(record => Path.GetFileName(record.Source), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group =>
            {
                var latestIngestedAt = group
                    .Select(record => TryParseDateTimeOffset(TryGetStringMetadata(record, DocumentVersioning.IngestedAtUtcMetadataKey)))
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return new SourceListItemDto
                {
                    Source = group.Key,
                    KnowledgeBaseId = normalizedKnowledgeBaseId,
                    ChunkCount = group.Count(),
                    DocumentVersion = group
                        .Select(record => TryGetStringMetadata(record, DocumentVersioning.DocumentVersionMetadataKey))
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    SourceChecksum = group
                        .Select(record => TryGetStringMetadata(record, DocumentVersioning.SourceChecksumMetadataKey))
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    IngestedAtUtc = latestIngestedAt == default ? null : latestIngestedAt
                };
            })
            .OrderByDescending(item => item.IngestedAtUtc)
            .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(sources);
    }

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public async Task<ActionResult<SourceDeleteResponse>> Delete(
        [FromQuery] string source,
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest(ApiErrorFactory.BadRequest("Source is required.", "Query parameter 'source' is required."));
        }

        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var normalizedSource = Path.GetFileName(source.Trim());
        var records = await vectorStoreService.LoadAsync(cancellationToken);

        var matchedRecords = records
            .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId)
                && string.Equals(Path.GetFileName(record.Source), normalizedSource, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchedRecords.Count == 0)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Source document not found.",
                $"No ingested chunks were found for source '{normalizedSource}'."));
        }

        await vectorStoreService.DeleteByIdsAsync(matchedRecords.Select(record => record.Id), cancellationToken);

        return Ok(new SourceDeleteResponse
        {
            Source = normalizedSource,
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            RemovedChunks = matchedRecords.Count,
            Message = $"Removed {matchedRecords.Count} chunk(s) for source '{normalizedSource}'."
        });
    }

    [HttpGet("document")]
    public async Task<ActionResult<SourceDocumentResponse>> GetDocument(
        [FromQuery] string source,
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest(ApiErrorFactory.BadRequest("Source is required.", "Query parameter 'source' is required."));
        }

        var document = await sourceDocumentViewerService.GetDocumentAsync(source, knowledgeBaseId, cancellationToken);
        if (document is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Source document not found.",
                $"No ingested chunks were found for source '{Path.GetFileName(source)}'."));
        }

        return Ok(document);
    }

    [HttpGet("update-alert")]
    public async Task<ActionResult<SourceUpdateAlertResponse>> GetUpdateAlert(
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var resolvedSourcePath = ResolveSourcePath(challengeOptions.Value.SourceDocumentPath, environment.ContentRootPath);

        if (!System.IO.File.Exists(resolvedSourcePath))
        {
            return Ok(new SourceUpdateAlertResponse
            {
                KnowledgeBaseId = normalizedKnowledgeBaseId,
                RequiresReingestReview = false,
                CurrentSourceChecksum = null,
                IngestedSourceChecksum = null,
                DetectedAtUtc = DateTimeOffset.UtcNow,
                Message = "Source document is not available on disk."
            });
        }

        var sourceContent = await System.IO.File.ReadAllTextAsync(resolvedSourcePath, cancellationToken);
        var currentChecksum = DocumentVersioning.ComputeSourceChecksum(sourceContent);

        var records = await vectorStoreService.LoadAsync(cancellationToken);
        var checksumCandidates = records
            .Where(record => string.Equals(KnowledgeBaseScope.GetRecordKnowledgeBaseId(record), normalizedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Metadata.TryGetValue(DocumentVersioning.SourceChecksumMetadataKey, out var checksum) ? checksum : null)
            .Where(checksum => !string.IsNullOrWhiteSpace(checksum))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ingestedChecksum = checksumCandidates.Count > 0 ? checksumCandidates[0] : null;
        var requiresReview = ingestedChecksum is not null
            && !string.Equals(ingestedChecksum, currentChecksum, StringComparison.OrdinalIgnoreCase);

        return Ok(new SourceUpdateAlertResponse
        {
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            RequiresReingestReview = requiresReview,
            CurrentSourceChecksum = currentChecksum,
            IngestedSourceChecksum = ingestedChecksum,
            DetectedAtUtc = DateTimeOffset.UtcNow,
            Message = requiresReview
                ? "Source document checksum changed since the last ingest. Reingest/review is recommended."
                : "No source update alert detected."
        });
    }

    [HttpGet("compare")]
    public async Task<ActionResult<SourceComparisonResponse>> GetComparison(
        [FromQuery] string? source,
        [FromQuery] string? knowledgeBaseId,
        [FromQuery] string? citationChunkId,
        [FromQuery] bool includeUnchanged,
        CancellationToken cancellationToken)
    {
        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var resolvedSourcePath = ResolveSourcePath(challengeOptions.Value.SourceDocumentPath, environment.ContentRootPath);
        if (!System.IO.File.Exists(resolvedSourcePath))
        {
            return NotFound(ApiErrorFactory.NotFound("Source document not found.", "Configured source document does not exist on disk."));
        }

        var sourceName = string.IsNullOrWhiteSpace(source)
            ? Path.GetFileName(resolvedSourcePath)
            : Path.GetFileName(source.Trim());
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return BadRequest(ApiErrorFactory.BadRequest("Source is required.", "Query parameter 'source' is required."));
        }

        var sourceText = await documentExtractionService.ExtractTextFromFileAsync(resolvedSourcePath, cancellationToken);
        var currentChunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        var currentVersion = DocumentVersioning.ComputeDefaultVersionLabel(DocumentVersioning.ComputeSourceChecksum(sourceText));

        var allRecords = await vectorStoreService.LoadAsync(cancellationToken);
        var ingestedRecords = allRecords
            .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId)
                && string.Equals(record.Source, sourceName, StringComparison.OrdinalIgnoreCase))
            .Select(record => new
            {
                Record = record,
                Index = TryGetIntMetadata(record, "Index"),
                SectionTitle = TryGetStringMetadata(record, "SectionTitle") ?? string.Empty,
                StartLine = TryGetIntMetadata(record, "StartLine"),
                EndLine = TryGetIntMetadata(record, "EndLine"),
                DocumentVersion = TryGetStringMetadata(record, DocumentVersioning.DocumentVersionMetadataKey)
            })
            .ToList();

        var currentByKey = currentChunks.ToDictionary(
            chunk => BuildComparisonKey(chunk.Index, chunk.SectionTitle),
            chunk => chunk,
            StringComparer.OrdinalIgnoreCase);
        var ingestedByKey = ingestedRecords.ToDictionary(
            item => BuildComparisonKey(item.Index, item.SectionTitle),
            item => item,
            StringComparer.OrdinalIgnoreCase);
        var allKeys = currentByKey.Keys
            .Concat(ingestedByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var comparisons = new List<SourceComparisonChunkDto>();
        foreach (var key in allKeys)
        {
            var hasCurrent = currentByKey.TryGetValue(key, out var currentChunk);
            var hasIngested = ingestedByKey.TryGetValue(key, out var ingestedChunk);

            var changeType = hasCurrent switch
            {
                true when !hasIngested => "added",
                false when hasIngested => "removed",
                true when hasIngested && string.Equals(currentChunk!.Content, ingestedChunk!.Record.ChunkText, StringComparison.Ordinal)
                    => "unchanged",
                _ => "modified"
            };

            if (!includeUnchanged && string.Equals(changeType, "unchanged", StringComparison.Ordinal))
            {
                continue;
            }

            var isImpactedCitation = !string.IsNullOrWhiteSpace(citationChunkId)
                && (string.Equals(citationChunkId, ingestedChunk?.Record.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(citationChunkId, currentChunk?.Id, StringComparison.OrdinalIgnoreCase));

            comparisons.Add(new SourceComparisonChunkDto
            {
                Index = hasCurrent ? currentChunk!.Index : ingestedChunk?.Index,
                SectionTitle = hasCurrent
                    ? currentChunk!.SectionTitle ?? string.Empty
                    : ingestedChunk?.SectionTitle ?? string.Empty,
                StartLine = hasCurrent ? currentChunk!.StartLine : ingestedChunk?.StartLine,
                EndLine = hasCurrent ? currentChunk!.EndLine : ingestedChunk?.EndLine,
                IngestedChunkId = ingestedChunk?.Record.Id,
                CurrentChunkId = currentChunk?.Id,
                ChangeType = changeType,
                IsImpactedCitation = isImpactedCitation,
                IngestedContent = ingestedChunk?.Record.ChunkText,
                CurrentContent = currentChunk?.Content
            });
        }

        var orderedComparisons = comparisons
            .OrderByDescending(item => item.IsImpactedCitation)
            .ThenBy(item => item.Index ?? int.MaxValue)
            .ThenBy(item => item.SectionTitle, StringComparer.Ordinal)
            .ToList();

        var changedCount = orderedComparisons.Count(item => !string.Equals(item.ChangeType, "unchanged", StringComparison.Ordinal));

        return Ok(new SourceComparisonResponse
        {
            Source = sourceName,
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            IngestedDocumentVersion = ingestedRecords
                .Select(item => item.DocumentVersion)
                .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version)),
            CurrentDocumentVersion = currentVersion,
            ChangedChunkCount = changedCount,
            TotalComparedChunks = allKeys.Count,
            Chunks = orderedComparisons
        });
    }

    [HttpGet("quality")]
    public async Task<ActionResult<SourceQualityReportResponse>> GetQuality(
        [FromQuery] string? source,
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var resolvedSourcePath = ResolveSourcePath(challengeOptions.Value.SourceDocumentPath, environment.ContentRootPath);
        var sourceName = string.IsNullOrWhiteSpace(source)
            ? Path.GetFileName(resolvedSourcePath)
            : Path.GetFileName(source.Trim());

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return BadRequest(ApiErrorFactory.BadRequest("Source is required.", "Query parameter 'source' is required."));
        }

        var allRecords = await vectorStoreService.LoadAsync(cancellationToken);
        var records = allRecords
            .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId)
                && string.Equals(record.Source, sourceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sectionGroups = records
            .Select(record => (Title: TryGetStringMetadata(record, "SectionTitle")?.Trim() ?? string.Empty, Record: record))
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicateSectionCount = sectionGroups.Count(group => group.Count() > 1);

        var weakExtractionZoneCount = records.Count(record =>
            record.ChunkText.Trim().Length < 80
            || string.IsNullOrWhiteSpace(TryGetStringMetadata(record, "SectionTitle")));

        var orderedByLength = records
            .Select(record => new
            {
                Record = record,
                CharCount = record.ChunkText.Trim().Length,
                SectionTitle = TryGetStringMetadata(record, "SectionTitle") ?? string.Empty,
                StartLine = TryGetIntMetadata(record, "StartLine"),
                EndLine = TryGetIntMetadata(record, "EndLine")
            })
            .ToList();

        return Ok(new SourceQualityReportResponse
        {
            Source = sourceName,
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            TotalChunks = records.Count,
            DuplicateSectionCount = duplicateSectionCount,
            WeakExtractionZoneCount = weakExtractionZoneCount,
            ShortestChunks = orderedByLength
                .OrderBy(item => item.CharCount)
                .ThenBy(item => item.Record.Id, StringComparer.Ordinal)
                .Take(5)
                .Select(item => new SourceQualityOutlierDto
                {
                    ChunkId = item.Record.Id,
                    SectionTitle = item.SectionTitle,
                    CharacterCount = item.CharCount,
                    StartLine = item.StartLine,
                    EndLine = item.EndLine
                })
                .ToList(),
            LongestChunks = orderedByLength
                .OrderByDescending(item => item.CharCount)
                .ThenBy(item => item.Record.Id, StringComparer.Ordinal)
                .Take(5)
                .Select(item => new SourceQualityOutlierDto
                {
                    ChunkId = item.Record.Id,
                    SectionTitle = item.SectionTitle,
                    CharacterCount = item.CharCount,
                    StartLine = item.StartLine,
                    EndLine = item.EndLine
                })
                .ToList()
        });
    }

    private static string ResolveSourcePath(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(contentRootPath, configuredPath);
    }

    private static string BuildComparisonKey(int index, string? sectionTitle)
    {
        return $"{index}:{sectionTitle?.Trim() ?? string.Empty}";
    }

    private static string BuildComparisonKey(int? index, string? sectionTitle)
    {
        return $"{index?.ToString() ?? "na"}:{sectionTitle?.Trim() ?? string.Empty}";
    }

    private static int? TryGetIntMetadata(VectorRecord record, string key)
    {
        if (record.Metadata.TryGetValue(key, out var value) && int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? TryGetStringMetadata(VectorRecord record, string key)
    {
        return record.Metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static DateTimeOffset? TryParseDateTimeOffset(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}
