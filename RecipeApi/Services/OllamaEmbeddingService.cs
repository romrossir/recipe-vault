using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RecipeApi.Services;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaEmbeddingRequest(
            "qwen3-embedding",
            text);

        var response = await _httpClient.PostAsJsonAsync(
            "/api/embed",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaEmbeddingResponse>(
                cancellationToken);

        if (result?.Embeddings is null ||
            result.Embeddings.Count == 0)
        {
            throw new InvalidOperationException(
                "Ollama returned no embedding.");
        }

        return result.Embeddings[0];
    }

    private sealed record OllamaEmbeddingRequest(
        string Model,
        string Input);

    private sealed record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embeddings")]
        List<float[]> Embeddings);
}