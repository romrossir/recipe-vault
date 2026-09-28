using RecipeApi.Models;

namespace RecipeApi.Services;

public static class RecipeSearchTextBuilder
{
    public static string Build(Recipe recipe)
    {
        var ingredients = string.Join(
            "\n",
            recipe.Ingredients.Select(i =>
                $"{i.Quantity} {i.Unit} {i.Name}".Trim()));

        var steps = string.Join(
            "\n",
            recipe.Steps);

        return $"""
            Title:
            {recipe.Title}

            Ingredients:
            {ingredients}

            Steps:
            {steps}
            """;
    }
}