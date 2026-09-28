using System.Text.Json;
using MongoDB.Driver;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeImporter
{
    private readonly IMongoCollection<Recipe> _recipes;
    private readonly IEmbeddingService _embeddingService;

    public RecipeImporter(
        IMongoCollection<Recipe> recipes,
        IEmbeddingService embeddingService)
    {
        _recipes = recipes;
        _embeddingService = embeddingService;
    }

    public async Task ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var recipes = await JsonSerializer.DeserializeAsync<List<Recipe>>(
            stream,
            options,
            cancellationToken: cancellationToken);

        if (recipes is null || recipes.Count == 0)
        {
            Console.WriteLine("No recipes found.");
            return;
        }

        Console.WriteLine($"Found {recipes.Count} recipes.");

        foreach (var recipe in recipes)
        {
            Console.WriteLine($"Embedding: {recipe.Title}");

            recipe.SearchText =
                RecipeSearchTextBuilder.Build(recipe);

            recipe.Embedding =
                await _embeddingService.GenerateAsync(
                    recipe.SearchText,
                    cancellationToken);
        }

        await _recipes.InsertManyAsync(
            recipes,
            cancellationToken: cancellationToken);

        Console.WriteLine(
            $"Imported {recipes.Count} recipes.");
    }
}