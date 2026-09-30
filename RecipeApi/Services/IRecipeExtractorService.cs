using RecipeApi.Models;

namespace RecipeApi.Services;

/// <summary>
/// Extracts structured recipes from raw OCR text or images using an LLM.
/// </summary>
public interface IRecipeExtractorService
{
    /// <summary>
    /// Parses OCR text into structured recipes using the LLM.
    /// </summary>
    /// <param name="ocrText">The raw text extracted by OCR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Recipe>> ExtractFromTextAsync(string ocrText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts structured recipes directly from an image using a vision model.
    /// </summary>
    /// <param name="imageStream">The image content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Recipe>> ExtractFromImageAsync(Stream imageStream, CancellationToken cancellationToken = default);
}
