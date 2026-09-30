using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RecipeApi.Data;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeExtractorService : IRecipeExtractorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string TextExtractionPrompt = """
        Tu es un assistant spécialisé dans l'extraction de recettes de cuisine.
        À partir du texte OCR fourni, extrais toutes les recettes et retourne UNIQUEMENT un tableau JSON valide avec exactement ces propriétés pour chaque recette :
        [
          {
            "title": "string ou null",
            "ingredients": [
              {
                "quantity": "string ou null",
                "unit": "string ou null",
                "name": "string"
              }
            ],
            "steps": ["string"]
          }
        ]
        IMPORTANT : retranscris le texte exactement tel quel, mot pour mot. Ne résume pas, ne simplifie pas, ne condense pas les étapes ou les ingrédients.
        S'il n'y a aucune recette, retourne un tableau vide [].
        Ne retourne rien d'autre que le JSON.
        """;

    private const string VisionExtractionPrompt = """
        Regarde cette image d'une page de recette de cuisine.
        Extrais toutes les recettes visibles et retourne UNIQUEMENT un tableau JSON valide avec exactement ces propriétés pour chaque recette :
        [
          {
            "title": "string ou null",
            "ingredients": [
              {
                "quantity": "string ou null",
                "unit": "string ou null",
                "name": "string"
              }
            ],
            "steps": ["string"]
          }
        ]
        IMPORTANT : retranscris le texte exactement tel quel, mot pour mot. Ne résume pas, ne simplifie pas, ne condense pas les étapes ou les ingrédients.
        S'il n'y a aucune recette sur la page, retourne un tableau vide [].
        Ne retourne rien d'autre que le JSON.
        """;

    private readonly ILlmService _llmService;
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<RecipeExtractorService> _logger;

    public RecipeExtractorService(
        ILlmService llmService,
        IHttpClientFactory httpClientFactory,
        OllamaSettings settings,
        ILogger<RecipeExtractorService> logger)
    {
        _llmService = llmService;
        _httpClient = httpClientFactory.CreateClient("OllamaVision");
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Recipe>> ExtractFromTextAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting recipes from OCR text ({Length} chars).", ocrText.Length);

        var raw = await _llmService.GenerateAsync(TextExtractionPrompt, ocrText, cancellationToken);
        return ParseRecipesJson(raw);
    }

    public async Task<IReadOnlyList<Recipe>> ExtractFromImageAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting recipes from image using vision model '{Model}'.", _settings.VisionModel);

        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        var base64Image = Convert.ToBase64String(memoryStream.ToArray());

        var request = new
        {
            model = _settings.VisionModel,
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = VisionExtractionPrompt,
                    images = new[] { base64Image }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Ollama vision returned no response.");

        var raw = result.Message?.Content
            ?? throw new InvalidOperationException("Ollama vision returned an empty message.");

        return ParseRecipesJson(raw);
    }

    private IReadOnlyList<Recipe> ParseRecipesJson(string raw)
    {
        _logger.LogDebug("Raw LLM response: {Raw}", raw);
        var json = ExtractJson(raw);

        List<ExtractedRecipe> extracted;
        if (json.TrimStart().StartsWith('['))
        {
            extracted = JsonSerializer.Deserialize<List<ExtractedRecipe>>(json, JsonOptions)
                ?? [];
        }
        else
        {
            var single = JsonSerializer.Deserialize<ExtractedRecipe>(json, JsonOptions);
            extracted = single is not null ? [single] : [];
        }

        _logger.LogInformation("Extracted {Count} recipe(s).", extracted.Count);

        return extracted.Select(ToRecipe).ToList();
    }

    private static Recipe ToRecipe(ExtractedRecipe extracted)
    {
        return new Recipe
        {
            Title = extracted.Title ?? "Sans titre",
            Ingredients = extracted.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => new Ingredient
                {
                    Name = i.Name!,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                })
                .ToList(),
            Steps = extracted.Steps
        };
    }

    private static string ExtractJson(string text)
    {
        var fenceMatch = Regex.Match(text, @"```\w*\s*([\s\S]*?)\s*```");
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        var arrayMatch = Regex.Match(text, @"\[[\s\S]*\]");
        if (arrayMatch.Success)
            return arrayMatch.Value;

        var braceMatch = Regex.Match(text, @"\{[\s\S]*\}");
        if (braceMatch.Success)
            return braceMatch.Value;

        return text.Trim();
    }

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("content")] string Content);

    private sealed class ExtractedRecipe
    {
        public string? Title { get; set; }
        public List<ExtractedIngredient> Ingredients { get; set; } = [];
        public List<string> Steps { get; set; } = [];
    }

    private sealed class ExtractedIngredient
    {
        public string? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Name { get; set; }
    }
}
