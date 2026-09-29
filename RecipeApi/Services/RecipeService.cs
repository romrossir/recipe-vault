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

    public RecipeService(
        IMongoCollection<Recipe> recipes,
        IEmbeddingService embeddingService,
        MongoDbSettings settings)
    {
        _recipes = recipes;
        _embeddingService = embeddingService;
        _settings = settings;
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

        return recipe;
    }

    public async Task<bool> UpdateAsync(string id, Recipe recipe, CancellationToken cancellationToken = default)
    {
        recipe.Id = id;

        await GenerateEmbeddingAsync(recipe, cancellationToken);

        var result = await _recipes.ReplaceOneAsync(r => r.Id == id, recipe, cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var result = await _recipes.DeleteOneAsync(r => r.Id == id, cancellationToken);

        return result.DeletedCount > 0;
    }

    public async Task<IReadOnlyList<RecipeSearchResult>> SearchAsync(
        float[] queryVector, int limit, CancellationToken cancellationToken = default)
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