using RecipeApi.Models;

namespace RecipeApi.Services;

/// <summary>
/// Answers natural-language questions about recipes using RAG (retrieval-augmented generation).
/// </summary>
public interface IRecipeAssistantService
{
    /// <summary>
    /// Searches for relevant recipes and generates an answer grounded in their content.
    /// </summary>
    /// <param name="question">The user's question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated answer and the recipes used as context.</returns>
    Task<RecipeAssistantResult> AskAsync(string question, CancellationToken cancellationToken = default);
}
