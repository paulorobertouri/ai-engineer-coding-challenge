namespace Api.Services;

public sealed class DeterministicEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 128;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var vector = new float[Dimensions];

        foreach (var token in Tokenize(text))
        {
            var hash = ComputeFnv1a(token);
            var index = (int)(hash % Dimensions);
            var sign = (hash & 1) == 0 ? 1f : -1f;
            vector[index] += sign;
        }

        Normalize(vector);
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var chars = text.ToLowerInvariant().ToCharArray();
        var start = -1;

        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return new string(chars, start, i - start);
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return new string(chars, start, chars.Length - start);
        }
    }

    private static uint ComputeFnv1a(string token)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;

        uint hash = offset;
        foreach (var ch in token)
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash;
    }

    private static void Normalize(float[] vector)
    {
        double norm = 0;
        foreach (var value in vector)
        {
            norm += value * value;
        }

        if (norm <= 0)
        {
            return;
        }

        var scale = 1.0 / Math.Sqrt(norm);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] * scale);
        }
    }
}
