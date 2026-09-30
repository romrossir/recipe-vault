using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class SaveRecipeRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }

    public IReadOnlyList<IngredientDto> Ingredients { get; init; } = [];

    public IReadOnlyList<string> Steps { get; init; } = [];

    public string? SourceFileId { get; init; }

    public IReadOnlyList<int> SourcePages { get; init; } = [];
}
