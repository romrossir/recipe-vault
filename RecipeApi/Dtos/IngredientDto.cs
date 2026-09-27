namespace RecipeApi.Dtos;

public sealed record IngredientDto(
    string Name,
    string? Quantity,
    string? Unit);