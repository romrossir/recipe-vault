namespace RecipeApi.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);
}