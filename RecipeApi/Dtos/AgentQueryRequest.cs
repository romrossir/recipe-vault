using System.ComponentModel.DataAnnotations;

namespace RecipeApi.Dtos;

public sealed class AgentQueryRequest
{
    [Required]
    [StringLength(1000)]
    public required string Query { get; init; }
}
