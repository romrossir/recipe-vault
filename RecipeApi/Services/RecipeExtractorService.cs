using System.Text.Json;
using System.Text.RegularExpressions;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeExtractorService : IRecipeExtractorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SystemPrompt = """
        Tu es un assistant spécialisé dans l'extraction de recettes de cuisine.
        À partir du texte OCR fourni, extrais la recette et retourne UNIQUEMENT du JSON valide avec exactement ces propriétés :
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
        Ne retourne rien d'autre que le JSON.
        """;

    private readonly ILlmService _llmService;
    private readonly ILogger<RecipeExtractorService> _logger;

    public RecipeExtractorService(ILlmService llmService, ILogger<RecipeExtractorService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Recipe> ExtractAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting recipe from OCR text ({Length} chars).", ocrText.Length);

        var raw = await _llmService.GenerateAsync(SystemPrompt, ocrText, cancellationToken);
        var json = StripMarkdownCodeFence(raw);

        var extracted = JsonSerializer.Deserialize<ExtractedRecipe>(json, JsonOptions)
            ?? throw new InvalidOperationException("LLM returned invalid JSON for recipe extraction.");

        _logger.LogInformation("Extracted recipe '{Title}' with {Count} ingredient(s).", extracted.Title, extracted.Ingredients.Count);

        return ToRecipe(extracted);
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

    private static string StripMarkdownCodeFence(string text)
    {
        var trimmed = text.Trim();
        return Regex.Replace(trimmed, @"^```\w*\s*|\s*```$", "", RegexOptions.Multiline).Trim();
    }

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
