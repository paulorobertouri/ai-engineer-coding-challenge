using Api.Models;

namespace Api.Domain.Ingestion;

public static class IngestionDomainPolicy
{
    public static bool TryValidateUploadedText(string sourceText, int maxLineLength, out string message)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            message = "The uploaded file did not contain readable text content.";
            return false;
        }

        var lines = sourceText.Split('\n');
        if (lines.Any(line => line.TrimEnd('\r').Length > maxLineLength))
        {
            message = $"The uploaded file contains lines longer than {maxLineLength} characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool TryFindDuplicateVersion(
        IEnumerable<VectorRecord> existingRecords,
        string sourceChecksum,
        out string duplicateVersion)
    {
        var duplicate = existingRecords.FirstOrDefault(record =>
            record.Metadata.TryGetValue(DocumentVersioning.SourceChecksumMetadataKey, out var existingChecksum)
            && string.Equals(existingChecksum, sourceChecksum, StringComparison.Ordinal));

        if (duplicate is null)
        {
            duplicateVersion = "unknown";
            return false;
        }

        if (!duplicate.Metadata.TryGetValue(DocumentVersioning.DocumentVersionMetadataKey, out var version)
            || string.IsNullOrWhiteSpace(version))
        {
            duplicateVersion = "unknown";
        }
        else
        {
            duplicateVersion = version;
        }

        return true;
    }

    public static bool CanProceedWithIngest(int existingRecordCount, bool forceReingest)
    {
        return existingRecordCount == 0 || forceReingest;
    }
}