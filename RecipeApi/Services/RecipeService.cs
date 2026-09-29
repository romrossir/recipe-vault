using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeService : IRecipeService
{
    private readonly IMongoCollection<Recipe> _recipes;

    public RecipeService(IMongoCollection<Recipe> recipes)
    {
        _recipes = recipes;
    }

    public async Task<IReadOnlyCollection<Recipe>> GetAllAsync(
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _recipes
            .Find(FilterDefinition<Recipe>.Empty)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await _recipes
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Recipe> CreateAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default)
    {
        await _recipes.InsertOneAsync(
            recipe,
            cancellationToken: cancellationToken);

        return recipe;
    }

    public async Task<bool> UpdateAsync(
        string id,
        Recipe recipe,
        CancellationToken cancellationToken = default)
    {
        recipe.Id = id;

        var result = await _recipes.ReplaceOneAsync(
            r => r.Id == id,
            recipe,
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var result = await _recipes.DeleteOneAsync(
            r => r.Id == id,
            cancellationToken);

        return result.DeletedCount > 0;
    }

    public async Task<IReadOnlyList<(Recipe Recipe, double Score)>> SearchAsync(
        float[] queryVector,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var pipeline = new[]
        {
        new BsonDocument("$vectorSearch", new BsonDocument
        {
            { "index", "recipe_embedding_index" },
            { "path", "Embedding" },
            {
                "queryVector",
                new BsonArray(queryVector.Select(x => (double)x))
            },
            { "numCandidates", Math.Max(limit * 20, 100) },
            { "limit", limit }
        }),
        new BsonDocument("$project", new BsonDocument
        {
            { "_id", 1 },
            { "Title", 1 },
            { "Ingredients", 1 },
            { "Steps", 1 },
            {
                "Score",
                new BsonDocument("$meta", "vectorSearchScore")
            }
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

                var recipe = BsonSerializer.Deserialize<Recipe>(
                    document);

                return (recipe, score);
            })
            .ToList();
    }
}