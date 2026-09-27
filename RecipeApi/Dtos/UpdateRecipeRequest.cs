using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class UpdateRecipeRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }

    public IReadOnlyList<IngredientDto> Ingredients { get; init; } = [];

    public IReadOnlyList<string> Steps { get; init; } = [];
}