namespace RecipeApi.Models;

public class Ingredient
{
    public required string Name { get; set; }

    public string? Quantity { get; set; }

    public string? Unit { get; set; }
}
