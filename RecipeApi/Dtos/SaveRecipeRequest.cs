using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class SaveRecipeRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }

    public string? Author { get; init; }

    public string? PrepTime { get; init; }

    public string? CookTime { get; init; }

    public string? Servings { get; init; }

    public IReadOnlyList<IngredientDto> Ingredients { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? SourceFileId { get; init; }

    public IReadOnlyList<int> SourcePages { get; init; } = [];
}
