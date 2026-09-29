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
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest(
            _settings.LlmModel,
            [
                new OllamaChatMessage("system", systemPrompt),
                new OllamaChatMessage("user", userMessage)
            ],
            false);

        var response = await _httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaChatResponse>(
                cancellationToken);

        if (result?.Message is null)
        {
            throw new InvalidOperationException(
                "Ollama returned no response.");
        }

        return result.Message.Content;
    }

    private sealed record OllamaChatRequest(
        string Model,
        List<OllamaChatMessage> Messages,
        bool Stream);

    private sealed record OllamaChatMessage(
        string Role,
        string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")]
        OllamaChatMessage? Message);
}