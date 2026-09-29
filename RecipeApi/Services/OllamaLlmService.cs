using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RecipeApi.Data;

namespace RecipeApi.Services;

public sealed class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaLlmService(
        HttpClient httpClient,
        OllamaSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest(
            _settings.LlmModel,
            prompt,
            false);

        var response = await _httpClient.PostAsJsonAsync(
            "/api/generate",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaGenerateResponse>(
                cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Ollama returned no response.");
        }

        return result.Response;
    }

    private sealed record OllamaGenerateRequest(
        string Model,
        string Prompt,
        bool Stream);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")]
        string Response);
}