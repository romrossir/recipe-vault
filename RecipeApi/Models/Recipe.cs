namespace RecipeApi.Models;

public sealed class Recipe
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];

    public List<string> Steps { get; set; } = [];
}
