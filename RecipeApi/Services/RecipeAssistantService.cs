using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class RecipeAssistantService : IRecipeAssistantService
{
    private readonly IRecipeService _recipeService;
    private readonly ILlmService _llmService;

    public RecipeAssistantService(IRecipeService recipeService, ILlmService llmService)
    {
        _recipeService = recipeService;
        _llmService = llmService;
    }

    public async Task<RecipeAssistantResult> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var results = await _recipeService.SearchAsync(question, 5, cancellationToken);

        var context = string.Join(
            "\n\n--- RECIPE ---\n\n",
            results.Select(r => $"""
            Title: {r.Recipe.Title}

            Ingredients:
            {string.Join(
                    "\n",
                    r.Recipe.Ingredients.Select(i =>
                        $"- {i.Quantity} {i.Unit} {i.Name}".Trim()))}

            Steps:
            {string.Join("\n", r.Recipe.Steps)}
            """));

        var systemPrompt = """
        Tu es un assistant spécialisé dans les recettes de cuisine.

        Réponds à la question uniquement à partir des recettes
        présentes dans le contexte.

        Ne crée pas d'ingrédient, de recette ou d'information
        qui n'est pas présente dans le contexte.

        Si le contexte ne permet pas de répondre à la question,
        indique-le clairement.
        """;

        var userMessage = $"""
        Question :
        {question}

        Contexte :
        {context}
        """;

        var answer = await _llmService.GenerateAsync(systemPrompt, userMessage, cancellationToken);

        return new RecipeAssistantResult(answer, results);
    }
}
