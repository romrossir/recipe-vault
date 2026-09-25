namespace RecipeExtractor.Models;

public class Recipe
{
    public string? Title { get; set; }

    public int? Servings { get; set; }

    public int? PreparationTimeMinutes { get; set; }

    public int? CookingTimeMinutes { get; set; }

    public List<Ingredient> Ingredients { get; set; } = [];

    public List<string> Steps { get; set; } = [];
}