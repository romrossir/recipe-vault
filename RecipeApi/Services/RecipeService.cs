using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeService : IRecipeService
{
    private readonly List<Recipe> _recipes = [];
    private int _nextId = 1;

    public IReadOnlyCollection<Recipe> GetAll()
    {
        return _recipes;
    }

    public Recipe? GetById(int id)
    {
        return _recipes.FirstOrDefault(r => r.Id == id);
    }

    public Recipe Create(Recipe recipe)
    {
        recipe.Id = _nextId++;

        _recipes.Add(recipe);

        return recipe;
    }

    public bool Update(int id, Recipe recipe)
    {
        var existing = GetById(id);

        if (existing is null)
        {
            return false;
        }

        existing.Title = recipe.Title;

        existing.Ingredients = recipe.Ingredients;
        existing.Steps = recipe.Steps;

        return true;
    }

    public bool Delete(int id)
    {
        var recipe = GetById(id);

        if (recipe is null)
        {
            return false;
        }

        _recipes.Remove(recipe);

        return true;
    }
}