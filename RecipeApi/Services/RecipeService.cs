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
        CancellationToken cancellationToken = default)
    {
        return await _recipes
            .Find(FilterDefinition<Recipe>.Empty)
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
}