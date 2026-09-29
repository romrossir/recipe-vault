using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class AskRecipeRequest
{
    [Required]
    [StringLength(1000)]
    public required string Question { get; init; }
}