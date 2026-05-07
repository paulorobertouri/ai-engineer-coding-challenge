namespace Api.Contracts;

public sealed class IngestRequest
{
    public bool ForceReingest { get; init; }
}