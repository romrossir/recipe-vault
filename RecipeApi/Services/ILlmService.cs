namespace RecipeApi.Services;

/// <summary>
/// Generates text completions from a large language model.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Sends a system prompt and user message to the LLM and returns the generated response.
    /// </summary>
    /// <param name="systemPrompt">Instructions that define the LLM's behavior.</param>
    /// <param name="userMessage">The user's message to respond to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated text response.</returns>
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}