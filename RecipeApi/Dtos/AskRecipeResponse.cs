namespace RecipeApi.Dtos;

public sealed record AskRecipeResponse(
    string Answer,
    IReadOnlyList<RecipeSearchResponse> Recipes);