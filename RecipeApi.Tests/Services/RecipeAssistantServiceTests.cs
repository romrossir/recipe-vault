using NSubstitute;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Tests.Services;

public sealed class RecipeAssistantServiceTests
{
    private readonly IRecipeService _recipeService = Substitute.For<IRecipeService>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly RecipeAssistantService _sut;

    public RecipeAssistantServiceTests()
    {
        _sut = new RecipeAssistantService(_recipeService, _llmService);
    }

    [Fact]
    public async Task AskAsync_ReturnsAnswerAndSources()
    {
        var searchResults = new List<RecipeSearchResult>
        {
            new(CreateRecipe("1", "Cheesecake"), 0.95),
            new(CreateRecipe("2", "Tarte Tatin"), 0.80)
        };

        _recipeService.SearchAsync("cheesecake", 5, Arg.Any<CancellationToken>())
            .Returns(searchResults);

        _llmService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Voici une recette de cheesecake.");

        var result = await _sut.AskAsync("cheesecake");

        Assert.Equal("Voici une recette de cheesecake.", result.Answer);
        Assert.Equal(2, result.Sources.Count);
    }

    [Fact]
    public async Task AskAsync_PassesRecipeContextToLlm()
    {
        var recipe = CreateRecipe("1", "Fondant au chocolat");
        recipe.Ingredients.Add(new Ingredient { Name = "chocolat", Quantity = "200", Unit = "g" });
        recipe.Tags.Add("dessert");

        _recipeService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<RecipeSearchResult> { new(recipe, 0.90) });

        _llmService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Réponse.");

        await _sut.AskAsync("chocolat");

        await _llmService.Received(1).GenerateAsync(
            Arg.Any<string>(),
            Arg.Is<string>(msg => msg.Contains("Fondant au chocolat") && msg.Contains("chocolat")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AskAsync_SystemPromptIsInFrench()
    {
        _recipeService.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<RecipeSearchResult>());

        _llmService.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Réponse.");

        await _sut.AskAsync("test");

        await _llmService.Received(1).GenerateAsync(
            Arg.Is<string>(prompt => prompt.Contains("recettes de cuisine")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static Recipe CreateRecipe(string id, string title)
    {
        return new Recipe { Id = id, Title = title };
    }
}
