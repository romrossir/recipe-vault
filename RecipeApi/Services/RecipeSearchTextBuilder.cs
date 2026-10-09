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

        var tags = string.Join(", ", recipe.Tags.Where(t => !string.IsNullOrWhiteSpace(t)));

        return $"""
        Title:
        {recipe.Title}

        Ingredients:
        {ingredients}

        Tags:
        {tags}
        """;
    }
}