using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using RecipeApi.Controllers;
using RecipeApi.Dtos;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Tests.Controllers;

public sealed class SourceFilesControllerTests
{
    private readonly ISourceFileService _sourceFileService = Substitute.For<ISourceFileService>();
    private readonly IMongoCollection<Recipe> _recipes = Substitute.For<IMongoCollection<Recipe>>();
    private readonly SourceFilesController _sut;

    public SourceFilesControllerTests()
    {
        _sut = new SourceFilesController(
            _sourceFileService,
            _recipes,
            NullLogger<SourceFilesController>.Instance);
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsCreated()
    {
        var file = CreateFormFile("test.pdf", "application/pdf", "content");
        var sourceFile = new SourceFile
        {
            Id = "sf-1",
            OriginalFileName = "test.pdf",
            StoredFileName = "abc.pdf",
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow
        };
        _sourceFileService.UploadAsync("test.pdf", "application/pdf", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(sourceFile);

        var result = await _sut.Upload(file, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<SourceFileResponse>(created.Value);
        Assert.Equal("sf-1", response.Id);
        Assert.Equal("test.pdf", response.OriginalFileName);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("empty.pdf", "application/pdf", "");

        var result = await _sut.Upload(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_ReturnsSourceFiles()
    {
        var sourceFiles = new List<SourceFile>
        {
            new() { Id = "1", OriginalFileName = "a.pdf", StoredFileName = "x.pdf", ContentType = "application/pdf" }
        };
        _sourceFileService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(sourceFiles);

        var result = await _sut.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<List<SourceFileResponse>>(ok.Value);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetById_Existing_ReturnsSourceFile()
    {
        var sourceFile = new SourceFile
        {
            Id = "1",
            OriginalFileName = "test.pdf",
            StoredFileName = "abc.pdf",
            ContentType = "application/pdf"
        };
        _sourceFileService.GetByIdAsync("1", Arg.Any<CancellationToken>()).Returns(sourceFile);

        var result = await _sut.GetById("1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SourceFileResponse>(ok.Value);
        Assert.Equal("test.pdf", response.OriginalFileName);
    }

    [Fact]
    public async Task GetById_NonExisting_ReturnsNotFound()
    {
        _sourceFileService.GetByIdAsync("999", Arg.Any<CancellationToken>()).Returns((SourceFile?)null);

        var result = await _sut.GetById("999", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetContent_NonExisting_ReturnsNotFound()
    {
        _sourceFileService.GetByIdAsync("999", Arg.Any<CancellationToken>()).Returns((SourceFile?)null);

        var result = await _sut.GetContent("999", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetContent_FileMissingOnDisk_ReturnsNotFound()
    {
        var sourceFile = new SourceFile
        {
            Id = "1",
            OriginalFileName = "test.pdf",
            StoredFileName = "abc.pdf",
            ContentType = "application/pdf"
        };
        _sourceFileService.GetByIdAsync("1", Arg.Any<CancellationToken>()).Returns(sourceFile);
        _sourceFileService.GetStoredFilePath("abc.pdf").Returns((string?)null);

        var result = await _sut.GetContent("1", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        _sourceFileService.DeleteAsync("1", Arg.Any<CancellationToken>()).Returns(true);
        var updateResult = Substitute.For<UpdateResult>();
        updateResult.ModifiedCount.Returns(0L);
        _recipes.UpdateManyAsync(
            Arg.Any<FilterDefinition<Recipe>>(),
            Arg.Any<UpdateDefinition<Recipe>>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(updateResult);

        var result = await _sut.Delete("1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_NonExisting_ReturnsNotFound()
    {
        _sourceFileService.DeleteAsync("999", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Delete("999", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var file = Substitute.For<IFormFile>();
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(stream.Length);
        file.OpenReadStream().Returns(stream);
        return file;
    }
}
