using System.Security.Cryptography;
using System.Text;

namespace Api.Models;

public static class DocumentVersioning
{
    public const string DocumentVersionMetadataKey = "DocumentVersion";
    public const string SourceChecksumMetadataKey = "SourceChecksum";
    public const string IngestedAtUtcMetadataKey = "IngestedAtUtc";

    public static string ComputeSourceChecksum(string sourceText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ComputeDefaultVersionLabel(string sourceChecksum)
    {
        var shortChecksum = sourceChecksum.Length > 12 ? sourceChecksum[..12] : sourceChecksum;
        return $"sha256:{shortChecksum}";
    }
}
