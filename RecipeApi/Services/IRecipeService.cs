using RecipeApi.Models;

namespace RecipeApi.Services;

/// <summary>
/// Manages recipe persistence, embedding generation, and semantic search.
/// </summary>
public interface IRecipeService
{
    /// <summary>
    /// Returns a paginated list of recipes.
    /// </summary>
    /// <param name="skip">Number of recipes to skip.</param>
    /// <param name="limit">Maximum number of recipes to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyCollection<Recipe>> GetAllAsync(int skip, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a recipe by its identifier, or <c>null</c> if not found.
    /// </summary>
    /// <param name="id">The recipe identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Recipe?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new recipe. Generates the search text and embedding automatically.
    /// </summary>
    /// <param name="recipe">The recipe to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created recipe with its generated identifier.</returns>
    Task<Recipe> CreateAsync(Recipe recipe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing recipe. Regenerates the search text and embedding automatically.
    /// </summary>
    /// <param name="id">The identifier of the recipe to update.</param>
    /// <param name="recipe">The new recipe data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the recipe was found and updated; <c>false</c> otherwise.</returns>
    Task<bool> UpdateAsync(string id, Recipe recipe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a recipe by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the recipe to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the recipe was found and deleted; <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a semantic vector search against recipe embeddings.
    /// </summary>
    /// <param name="queryVector">The query embedding vector.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recipes ranked by similarity score, highest first.</returns>
    Task<IReadOnlyList<RecipeSearchResult>> SearchAsync(
        float[] queryVector, int limit, CancellationToken cancellationToken = default);
}
