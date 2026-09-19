using System.Text.RegularExpressions;
using FileRepo.Core.VectorSearch;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Options;

namespace FileRepo.Infrastructure.VectorSearch;

public sealed partial class OnnxTextEmbeddingProvider : ITextEmbeddingProvider, IDisposable
{
    private const int MaxTokens = 512;
    private readonly InferenceSession session;
    private readonly IReadOnlyDictionary<string, long> vocabulary;

    public OnnxTextEmbeddingProvider(IOptions<VectorSearchOptions> options)
    {
        var settings = options.Value;
        var modelPath = Path.Combine(settings.ModelDirectory, "model.onnx");
        var vocabularyPath = Path.Combine(settings.ModelDirectory, "vocab.txt");
        if (!File.Exists(modelPath) || !File.Exists(vocabularyPath))
            throw new InvalidOperationException($"Embedding model assets are missing. Expected '{modelPath}' and '{vocabularyPath}'.");

        session = new InferenceSession(modelPath);
        vocabulary = File.ReadLines(vocabularyPath)
            .Select((token, index) => (token, index: (long)index))
            .ToDictionary(x => x.token, x => x.index, StringComparer.Ordinal);
        if (!vocabulary.ContainsKey("[CLS]") || !vocabulary.ContainsKey("[SEP]") || !vocabulary.ContainsKey("[UNK]"))
            throw new InvalidOperationException("Embedding vocabulary is missing required BERT special tokens.");
        ModelRevision = settings.ModelRevision;
        VectorSize = session.OutputMetadata.Values.First().Dimensions.Last();
        if (VectorSize <= 0) throw new InvalidOperationException("Embedding model has an invalid output vector dimension.");
    }

    public int VectorSize { get; }
    public string ModelRevision { get; }

    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(texts.Select(text => Embed(text, ct)).ToArray());

    public Task<ReadOnlyMemory<float>> EmbedQueryAsync(string queryText, CancellationToken ct) =>
        Task.FromResult<ReadOnlyMemory<float>>(Embed("Represent this sentence for searching relevant passages: " + queryText, ct));

    private ReadOnlyMemory<float> Embed(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var tokenIds = Tokenize(text);
        var inputIds = new DenseTensor<long>(new[] { 1, tokenIds.Length });
        var attentionMask = new DenseTensor<long>(new[] { 1, tokenIds.Length });
        var typeIds = new DenseTensor<long>(new[] { 1, tokenIds.Length });
        for (var i = 0; i < tokenIds.Length; i++) { inputIds[0, i] = tokenIds[i]; attentionMask[0, i] = 1; }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };
        if (session.InputMetadata.ContainsKey("token_type_ids"))
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", typeIds));
        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        var dimensions = output.Dimensions.ToArray();
        if (dimensions.Length != 3 || dimensions[2] != VectorSize)
            throw new InvalidOperationException("Embedding model output has an unexpected shape.");

        var vector = new float[VectorSize];
        for (var token = 0; token < tokenIds.Length; token++)
            for (var dimension = 0; dimension < VectorSize; dimension++)
                vector[dimension] += output[0, token, dimension];
        Normalize(vector);
        return vector;
    }

    private long[] Tokenize(string text)
    {
        var tokens = new List<long> { vocabulary["[CLS]"] };
        foreach (Match match in TokenPattern().Matches(text.ToLowerInvariant()))
        {
            foreach (var id in WordPiece(match.Value))
            {
                if (tokens.Count >= MaxTokens - 1) break;
                tokens.Add(id);
            }
            if (tokens.Count >= MaxTokens - 1) break;
        }
        tokens.Add(vocabulary["[SEP]"]);
        return tokens.ToArray();
    }

    private IEnumerable<long> WordPiece(string token)
    {
        if (vocabulary.TryGetValue(token, out var exact)) return [exact];
        var pieces = new List<long>();
        for (var start = 0; start < token.Length;)
        {
            long? match = null;
            var next = start;
            for (var end = token.Length; end > start; end--)
            {
                var candidate = (start == 0 ? "" : "##") + token[start..end];
                if (vocabulary.TryGetValue(candidate, out var id)) { match = id; next = end; break; }
            }
            if (match is null) return [vocabulary["[UNK]"]];
            pieces.Add(match.Value);
            start = next;
        }
        return pieces;
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        if (magnitude == 0 || !float.IsFinite(magnitude))
            throw new InvalidOperationException("Embedding model produced an invalid vector.");
        for (var i = 0; i < vector.Length; i++) vector[i] /= magnitude;
    }

    public void Dispose() => session.Dispose();

    [GeneratedRegex(@"[\p{L}\p{N}]+|[^\s\p{L}\p{N}]", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
