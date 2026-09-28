namespace RecipeApi.Models;

public sealed class RecipeSearchResult
{
    public required Recipe Recipe { get; init; }

    public double Score { get; init; }
}