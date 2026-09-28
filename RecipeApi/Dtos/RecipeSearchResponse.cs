using RecipeApi.Dtos;

namespace RecipeApi.Dtos;

public sealed record RecipeSearchResponse(
    string Id,
    string Title,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Steps,
    double Score);