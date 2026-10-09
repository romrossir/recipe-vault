using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Tests.Services;

public sealed class RecipeSearchTextBuilderTests
{
    [Fact]
    public void Build_IncludesTitleAndIngredientNames()
    {
        var recipe = new Recipe
        {
            Title = "Gâteau au chocolat",
            Ingredients =
            [
                new Ingredient { Name = "chocolat", Quantity = "200", Unit = "g" },
                new Ingredient { Name = "beurre", Quantity = "100", Unit = "g" }
            ]
        };

        var result = RecipeSearchTextBuilder.Build(recipe);

        Assert.Contains("Gâteau au chocolat", result);
        Assert.Contains("chocolat", result);
        Assert.Contains("beurre", result);
    }

    [Fact]
    public void Build_SkipsBlankIngredientNames()
    {
        var recipe = new Recipe
        {
            Title = "Test",
            Ingredients =
            [
                new Ingredient { Name = "farine" },
                new Ingredient { Name = "" },
                new Ingredient { Name = "  " }
            ]
        };

        var result = RecipeSearchTextBuilder.Build(recipe);

        Assert.Contains("farine", result);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.DoesNotContain(lines, line => string.IsNullOrWhiteSpace(line));
    }

    [Fact]
    public void Build_HandlesEmptyIngredients()
    {
        var recipe = new Recipe { Title = "Crêpes" };

        var result = RecipeSearchTextBuilder.Build(recipe);

        Assert.Contains("Crêpes", result);
    }
}
