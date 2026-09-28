namespace RecipeApi.Dtos;

public sealed record RecipeResponse(
    string Id,
    string Title,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Steps
);
