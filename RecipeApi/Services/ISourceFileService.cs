using RecipeApi.Models;

namespace RecipeApi.Services;

/// <summary>
/// Manages source file storage on the local filesystem and metadata in MongoDB.
/// </summary>
public interface ISourceFileService
{
    /// <summary>
    /// Stores a file on disk and saves its metadata.
    /// </summary>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="stream">The file content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created source file metadata.</returns>
    Task<SourceFile> UploadAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a source file by its identifier, or <c>null</c> if not found.
    /// </summary>
    Task<SourceFile?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all source files.
    /// </summary>
    Task<IReadOnlyList<SourceFile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a source file (both metadata and file on disk).
    /// </summary>
    /// <returns><c>true</c> if the source file was found and deleted; <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full filesystem path for a stored file, or <c>null</c> if the file does not exist on disk.
    /// </summary>
    string? GetStoredFilePath(string storedFileName);
}
