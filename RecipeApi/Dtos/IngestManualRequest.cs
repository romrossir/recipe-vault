using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class IngestManualRequest
{
    [Required]
    public required IReadOnlyList<SaveRecipeRequest> Recipes { get; init; }
}
