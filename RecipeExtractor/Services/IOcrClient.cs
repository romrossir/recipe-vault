namespace RecipeExtractor.Services;

public interface IOcrClient
{
    Task<string> ExtractTextAsync(
        IFormFile image,
        CancellationToken cancellationToken = default);
}