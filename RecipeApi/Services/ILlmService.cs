namespace RecipeApi.Services;

public interface ILlmService
{
    Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}