using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RecipeApi.Services;

public sealed class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;

    public OllamaLlmService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest(
            "llama3.2",
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