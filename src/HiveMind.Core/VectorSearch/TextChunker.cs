namespace HiveMind.Core.VectorSearch;

public sealed class TextChunker(VectorSearchValidationRules rules) : ITextChunker
{
    public IReadOnlyList<VectorChunk> Chunk(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(normalized)) return [];

        var chunks = new List<VectorChunk>();
        var start = 0;
        while (start < normalized.Length)
        {
            while (start < normalized.Length && char.IsWhiteSpace(normalized[start])) start++;
            if (start >= normalized.Length) break;

            var maximumEnd = Math.Min(normalized.Length, start + rules.ChunkSizeCharacters);
            var end = maximumEnd == normalized.Length ? normalized.Length : FindBoundary(normalized, start, maximumEnd);
            if (end <= start) end = maximumEnd;

            var contentEnd = end;
            while (contentEnd > start && char.IsWhiteSpace(normalized[contentEnd - 1])) contentEnd--;
            if (contentEnd > start)
            {
                chunks.Add(new VectorChunk(chunks.Count, start, contentEnd, normalized[start..contentEnd]));
            }

            if (end >= normalized.Length) break;
            start = Math.Max(start + 1, end - rules.ChunkOverlapCharacters);
        }

        return chunks;
    }

    private static int FindBoundary(string text, int start, int maximumEnd)
    {
        for (var i = maximumEnd; i > start; i--)
        {
            if (text[i - 1] == '\n' && i > 1 && text[i - 2] == '\n') return i;
        }
        for (var i = maximumEnd; i > start; i--)
        {
            if (text[i - 1] is '\n' or ' ' or '\t') return i;
        }
        return maximumEnd;
    }
}
