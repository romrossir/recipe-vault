namespace RecipeApi.Models;

public sealed record RecipeSearchResult(
    Recipe Recipe,
    double Score);
