namespace RecipeApi.Dtos;

public sealed record RecipeResponse(
    int Id,
    string Title,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<string> Steps
);
