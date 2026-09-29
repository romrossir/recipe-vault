using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RecipeApi.Data;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeService : IRecipeService
{
    private readonly IMongoCollection<Recipe> _recipes;
    private readonly IEmbeddingService _embeddingService;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(
        IMongoCollection<Recipe> recipes,
        IEmbeddingService embeddingService,
        MongoDbSettings settings,
        ILogger<RecipeService> logger)
    {
        _recipes = recipes;
        _embeddingService = embeddingService;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Recipe>> GetAllAsync(int skip, int limit, CancellationToken cancellationToken = default)
    {
        return await _recipes
            .Find(FilterDefinition<Recipe>.Empty)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _recipes
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Recipe> CreateAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        await GenerateEmbeddingAsync(recipe, cancellationToken);
        await _recipes.InsertOneAsync(recipe, cancellationToken: cancellationToken);
        _logger.LogInformation("Created recipe '{Title}' with id {Id}.", recipe.Title, recipe.Id);

        return recipe;
    }

    public async Task<bool> UpdateAsync(string id, Recipe recipe, CancellationToken cancellationToken = default)
    {
        recipe.Id = id;
        await GenerateEmbeddingAsync(recipe, cancellationToken);
        var result = await _recipes.ReplaceOneAsync(r => r.Id == id, recipe, cancellationToken: cancellationToken);
        var found = result.MatchedCount > 0;

        if (found)
            _logger.LogInformation("Updated recipe {Id}.", id);
        else
            _logger.LogWarning("Update failed: recipe {Id} not found.", id);

        return found;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await _recipes.DeleteOneAsync(r => r.Id == id, cancellationToken);
        var found = result.DeletedCount > 0;

        if (found)
            _logger.LogInformation("Deleted recipe {Id}.", id);
        else
            _logger.LogWarning("Delete failed: recipe {Id} not found.", id);

        return found;
    }

    public async Task<IReadOnlyList<RecipeSearchResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching for '{Query}' (limit={Limit}).", query, limit);
        var queryVector = await _embeddingService.GenerateAsync(query, cancellationToken);

        return await SearchByVectorAsync(queryVector, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RecipeSearchResult>> SearchByVectorAsync(
        float[] queryVector, int limit, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", _settings.EmbeddingIndexName },
                { "path", "Embedding" },
                {
                    "queryVector",
                    new BsonArray(queryVector.Select(x => (double)x))
                },
                { "numCandidates", Math.Max(limit * 20, 100) },
                { "limit", limit }
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                {
                    "Score",
                    new BsonDocument("$meta", "vectorSearchScore")
                }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { "Embedding", 0 },
                { "SearchText", 0 }
            })
        };

        var documents = await _recipes
            .Aggregate<BsonDocument>(pipeline)
            .ToListAsync(cancellationToken);

        return documents
            .Select(document =>
            {
                var score = document["Score"].AsDouble;
                document.Remove("Score");

                var recipe = BsonSerializer.Deserialize<Recipe>(document);

                return new RecipeSearchResult(recipe, score);
            })
            .ToList();
    }

    private async Task GenerateEmbeddingAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        recipe.SearchText = RecipeSearchTextBuilder.Build(recipe);

        recipe.Embedding = await _embeddingService.GenerateAsync(recipe.SearchText, cancellationToken);
    }
}