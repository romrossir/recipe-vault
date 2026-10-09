namespace RecipeApi.Models;

public sealed record RecipeAgentResult(
    string Answer,
    IReadOnlyList<RecipeSearchResult> Results);
