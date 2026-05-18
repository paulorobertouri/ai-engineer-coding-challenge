using Api.Models;

namespace Api.Services;

public interface IChunkingService
{
    Task<IReadOnlyList<TextChunk>> ChunkAsync(string sourceText, string sourceName, CancellationToken cancellationToken = default);
}