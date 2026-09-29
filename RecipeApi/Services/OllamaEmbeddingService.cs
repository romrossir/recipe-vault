using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RecipeApi.Data;

namespace RecipeApi.Services;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        OllamaSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<float[]> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaEmbeddingRequest(
            _settings.EmbeddingModel,
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