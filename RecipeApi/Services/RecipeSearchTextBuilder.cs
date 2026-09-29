using RecipeApi.Models;

namespace RecipeApi.Services;

public static class RecipeSearchTextBuilder
{
    public static string Build(Recipe recipe)
    {
        var ingredients = string.Join(
            "\n",
            recipe.Ingredients
                .Select(i => i.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)));

        return $"""
        Title:
        {recipe.Title}

        Ingredients:
        {ingredients}
        """;
    }
}