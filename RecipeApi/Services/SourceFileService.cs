using MongoDB.Driver;
using RecipeApi.Data;
using RecipeApi.Models;

namespace RecipeApi.Services;

public sealed class SourceFileService : ISourceFileService
{
    private readonly IMongoCollection<SourceFile> _sourceFiles;
    private readonly SourceFileSettings _settings;
    private readonly ILogger<SourceFileService> _logger;

    public SourceFileService(
        IMongoCollection<SourceFile> sourceFiles,
        SourceFileSettings settings,
        ILogger<SourceFileService> logger)
    {
        _sourceFiles = sourceFiles;
        _settings = settings;
        _logger = logger;
    }

    public async Task<SourceFile> UploadAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var storedFileName = $"{Guid.NewGuid()}{extension}";

        Directory.CreateDirectory(_settings.StoragePath);
        var filePath = Path.Combine(_settings.StoragePath, storedFileName);

        await using (var fileStream = File.Create(filePath))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var sourceFile = new SourceFile
        {
            OriginalFileName = fileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            UploadedAt = DateTime.UtcNow
        };

        await _sourceFiles.InsertOneAsync(sourceFile, cancellationToken: cancellationToken);
        _logger.LogInformation("Uploaded source file '{FileName}' as {StoredFileName} with id {Id}.", fileName, storedFileName, sourceFile.Id);

        return sourceFile;
    }

    public async Task<SourceFile?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _sourceFiles
            .Find(s => s.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SourceFile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _sourceFiles
            .Find(FilterDefinition<SourceFile>.Empty)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var sourceFile = await GetByIdAsync(id, cancellationToken);

        if (sourceFile is null)
            return false;

        var filePath = Path.Combine(_settings.StoragePath, sourceFile.StoredFileName);

        if (File.Exists(filePath))
            File.Delete(filePath);

        await _sourceFiles.DeleteOneAsync(s => s.Id == id, cancellationToken);
        _logger.LogInformation("Deleted source file {Id} ({FileName}).", id, sourceFile.OriginalFileName);

        return true;
    }

    public string? GetStoredFilePath(string storedFileName)
    {
        var filePath = Path.Combine(_settings.StoragePath, storedFileName);
        return File.Exists(filePath) ? filePath : null;
    }
}
