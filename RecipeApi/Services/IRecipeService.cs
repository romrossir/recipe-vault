using RecipeApi.Models;

namespace RecipeApi.Services;

public interface IRecipeService
{
    Task<IReadOnlyCollection<Recipe>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Recipe?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<Recipe> CreateAsync(
        Recipe recipe,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        string id,
        Recipe recipe,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);
}
