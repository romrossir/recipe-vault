using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using RecipeApi.Data;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Tests.Services;

public sealed class SourceFileServiceTests : IDisposable
{
    private readonly string _storagePath;
    private readonly IMongoCollection<SourceFile> _collection = Substitute.For<IMongoCollection<SourceFile>>();
    private readonly SourceFileService _sut;

    public SourceFileServiceTests()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), $"source-file-tests-{Guid.NewGuid()}");
        var settings = new SourceFileSettings { StoragePath = _storagePath };
        _sut = new SourceFileService(_collection, settings, NullLogger<SourceFileService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);
    }

    [Fact]
    public async Task UploadAsync_SavesFileToDisk()
    {
        using var stream = new MemoryStream("hello"u8.ToArray());

        var result = await _sut.UploadAsync("test.pdf", "application/pdf", stream);

        Assert.Equal("test.pdf", result.OriginalFileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.EndsWith(".pdf", result.StoredFileName);

        var filePath = Path.Combine(_storagePath, result.StoredFileName);
        Assert.True(File.Exists(filePath));
        Assert.Equal("hello", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task UploadAsync_InsertsIntoCollection()
    {
        using var stream = new MemoryStream("data"u8.ToArray());

        await _sut.UploadAsync("photo.jpg", "image/jpeg", stream);

        await _collection.Received(1).InsertOneAsync(
            Arg.Is<SourceFile>(s => s.OriginalFileName == "photo.jpg" && s.ContentType == "image/jpeg"),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetStoredFilePath_ExistingFile_ReturnsPath()
    {
        Directory.CreateDirectory(_storagePath);
        var fileName = "existing.pdf";
        File.WriteAllText(Path.Combine(_storagePath, fileName), "content");

        var result = _sut.GetStoredFilePath(fileName);

        Assert.NotNull(result);
        Assert.Equal(Path.Combine(_storagePath, fileName), result);
    }

    [Fact]
    public void GetStoredFilePath_NonExistingFile_ReturnsNull()
    {
        var result = _sut.GetStoredFilePath("missing.pdf");

        Assert.Null(result);
    }
}
