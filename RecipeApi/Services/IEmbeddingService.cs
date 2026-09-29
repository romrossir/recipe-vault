namespace RecipeApi.Services;

/// <summary>
/// Generates vector embeddings from text for semantic search.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding vector as a float array.</returns>
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default);
}