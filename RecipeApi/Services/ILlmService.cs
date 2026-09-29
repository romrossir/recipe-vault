namespace RecipeApi.Services;

public interface ILlmService
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}