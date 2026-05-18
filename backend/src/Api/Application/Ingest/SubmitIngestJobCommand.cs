using Api.Models;

namespace Api.Application.Ingest;

public sealed record SubmitIngestJobCommand(
    string SourceText,
    string SourceName,
    string DisplayPath,
    string KnowledgeBaseId,
    IReadOnlyList<VectorRecord> AllExistingRecords,
    IReadOnlyList<VectorRecord> ExistingRecords,
    string Action,
    string? PrecomputedSourceChecksum,
    bool ForceReingest);