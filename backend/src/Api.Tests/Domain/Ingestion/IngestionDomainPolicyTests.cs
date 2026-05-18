using Api.Domain.Ingestion;
using Api.Models;

namespace Api.Tests.Domain.Ingestion;

public sealed class IngestionDomainPolicyTests
{
    [Fact(DisplayName = "Given a text-only upload when content is blank then the upload is rejected")]
    public void GivenBlankUpload_WhenValidatingText_ThenValidationFails()
    {
        var isValid = IngestionDomainPolicy.TryValidateUploadedText("   ", 10_000, out var message);

        Assert.False(isValid);
        Assert.Contains("did not contain readable text", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Given a text-only upload when a line exceeds limit then the upload is rejected")]
    public void GivenOverlongUploadLine_WhenValidatingText_ThenValidationFails()
    {
        var content = new string('x', 12);

        var isValid = IngestionDomainPolicy.TryValidateUploadedText(content, 10, out var message);

        Assert.False(isValid);
        Assert.Contains("lines longer than 10 characters", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Given an existing checksum with known version when checking duplicates then duplicate is detected")]
    public void GivenExistingChecksumWithVersion_WhenFindingDuplicate_ThenReturnsVersion()
    {
        const string checksum = "sha256:abc";
        var existingRecords = new[]
        {
            new VectorRecord
            {
                Id = "existing-1",
                Source = "SOP.md",
                ChunkText = "content",
                Embedding = [0.25f],
                Metadata = new Dictionary<string, string>
                {
                    [DocumentVersioning.SourceChecksumMetadataKey] = checksum,
                    [DocumentVersioning.DocumentVersionMetadataKey] = "sha256:v1"
                }
            }
        };

        var isDuplicate = IngestionDomainPolicy.TryFindDuplicateVersion(existingRecords, checksum, out var duplicateVersion);

        Assert.True(isDuplicate);
        Assert.Equal("sha256:v1", duplicateVersion);
    }

    [Fact(DisplayName = "Given an existing checksum without explicit version when checking duplicates then duplicate version is unknown")]
    public void GivenExistingChecksumWithoutVersion_WhenFindingDuplicate_ThenReturnsUnknownVersion()
    {
        const string checksum = "sha256:abc";
        var existingRecords = new[]
        {
            new VectorRecord
            {
                Id = "existing-1",
                Source = "SOP.md",
                ChunkText = "content",
                Embedding = [0.25f],
                Metadata = new Dictionary<string, string>
                {
                    [DocumentVersioning.SourceChecksumMetadataKey] = checksum
                }
            }
        };

        var isDuplicate = IngestionDomainPolicy.TryFindDuplicateVersion(existingRecords, checksum, out var duplicateVersion);

        Assert.True(isDuplicate);
        Assert.Equal("unknown", duplicateVersion);
    }

    [Theory(DisplayName = "Given records in the knowledge base when evaluating reingest rules then only force reingest can proceed")]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(1, true, true)]
    public void GivenExistingRecordCount_WhenEvaluatingIngestEligibility_ThenRespectsForceReingestRule(
        int existingRecordCount,
        bool forceReingest,
        bool expected)
    {
        var canProceed = IngestionDomainPolicy.CanProceedWithIngest(existingRecordCount, forceReingest);

        Assert.Equal(expected, canProceed);
    }
}