namespace RecipeApi.Models;

public class Ingredient
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Quantity { get; set; }

    public string? Unit { get; set; }
}
