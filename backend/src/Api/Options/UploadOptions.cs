using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    [Range(1024, 104857600)]
    public long MaxUploadBytes { get; init; } = 10 * 1024 * 1024;
}
