using RecipeApi.Models;

namespace RecipeApi.Services;

public interface IRecipeService
{
    IReadOnlyCollection<Recipe> GetAll();

    Recipe? GetById(int id);

    Recipe Create(Recipe recipe);

    bool Update(int id, Recipe recipe);

    bool Delete(int id);
}
