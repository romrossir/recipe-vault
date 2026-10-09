namespace RecipeApi.Dtos;

public sealed record RecipeSearchResponse(
    string Id,
    string Title,
    string? Author,
    string? PrepTime,
    string? CookTime,
    string? Servings,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Tags,
    string? SourceFileId,
    IReadOnlyList<int> SourcePages,
    double Score);