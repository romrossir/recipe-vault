namespace RecipeApi.Models;

public sealed record RecipeAssistantResult(
    string Answer,
    IReadOnlyList<RecipeSearchResult> Sources);
