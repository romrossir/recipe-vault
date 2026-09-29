using System.Text.Json;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeImporter
{
    private readonly IMongoCollection<Recipe> _recipes;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RecipeImporter> _logger;

    public RecipeImporter(
        IMongoCollection<Recipe> recipes,
        IEmbeddingService embeddingService,
        ILogger<RecipeImporter> logger)
    {
        _recipes = recipes;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var recipes = await JsonSerializer.DeserializeAsync<List<Recipe>>(stream, options, cancellationToken: cancellationToken);

        if (recipes is null || recipes.Count == 0)
        {
            _logger.LogWarning("No recipes found in {FilePath}.", filePath);
            return;
        }

        _logger.LogInformation("Found {Count} recipes.", recipes.Count);

        foreach (var recipe in recipes)
        {
            _logger.LogInformation("Embedding: {Title}", recipe.Title);
            recipe.SearchText = RecipeSearchTextBuilder.Build(recipe);
            recipe.Embedding = await _embeddingService.GenerateAsync(recipe.SearchText, cancellationToken);
        }

        await _recipes.InsertManyAsync(recipes, cancellationToken: cancellationToken);
        _logger.LogInformation("Imported {Count} recipes.", recipes.Count);
    }
}