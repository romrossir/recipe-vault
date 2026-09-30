namespace RecipeApi.Services;

/// <summary>
/// Extracts text from images via an external OCR service.
/// </summary>
public interface IOcrClient
{
    /// <summary>
    /// Sends an image to the OCR service and returns the extracted text.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="stream">The file content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string> ExtractTextAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default);
}
