namespace RecipeApi.Dtos;

public sealed record RecipeSearchResponse(
    string Id,
    string Title,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Steps,
    string? SourceFileId,
    IReadOnlyList<int> SourcePages,
    double Score);