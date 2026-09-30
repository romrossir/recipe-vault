using RecipeApi.Models;

namespace RecipeApi.Services;

/// <summary>
/// Extracts a structured recipe from raw OCR text using an LLM.
/// </summary>
public interface IRecipeExtractorService
{
    /// <summary>
    /// Parses OCR text into a structured recipe.
    /// </summary>
    /// <param name="ocrText">The raw text extracted by OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Recipe> ExtractAsync(string ocrText, CancellationToken cancellationToken = default);
}
