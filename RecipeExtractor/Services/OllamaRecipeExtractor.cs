using System.Net.Http.Json;
using System.Text.Json;
using RecipeExtractor.Models;

namespace RecipeExtractor.Services;

public class OllamaRecipeExtractor : IRecipeExtractor
{
    private readonly HttpClient _httpClient;

    public OllamaRecipeExtractor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Recipe> ExtractAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Extract the recipe from the following OCR text.

            Return ONLY valid JSON.
            The JSON must have exactly these properties:
            - title: string or null
            - servings: number or null
            - preparationTimeMinutes: number or null
            - cookingTimeMinutes: number or null
            - ingredients: array of objects with quantity, unit and name
            - steps: array of strings

            Do not use Markdown.
            Do not add explanations.

            OCR text:
            {text}
            """;

        var request = new
        {
            model = "gemma4:e4b",
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var ollamaResponse =
            await response.Content.ReadFromJsonAsync<OllamaResponse>(
                cancellationToken);

        if (ollamaResponse?.Message?.Content == null)
        {
            throw new InvalidOperationException(
                "Ollama returned an empty response.");
        }

        var recipe = JsonSerializer.Deserialize<Recipe>(
            ollamaResponse.Message.Content,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return recipe
            ?? throw new InvalidOperationException(
                "Could not deserialize the recipe returned by Ollama.");
    }

    private class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
    }

    private class OllamaMessage
    {
        public string? Content { get; set; }
    }
}