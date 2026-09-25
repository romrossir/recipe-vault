using RecipeExtractor.Models;

namespace RecipeExtractor.Services;

public interface IRecipeExtractor
{
    Task<Recipe> ExtractAsync(
        string text,
        CancellationToken cancellationToken = default);
}