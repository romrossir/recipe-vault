using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using RecipeApi.Dtos;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Controllers;

[ApiController]
[Route("api/sources")]
public sealed class SourceFilesController : ControllerBase
{
    private readonly ISourceFileService _sourceFileService;
    private readonly IMongoCollection<Recipe> _recipes;
    private readonly ILogger<SourceFilesController> _logger;

    public SourceFilesController(
        ISourceFileService sourceFileService,
        IMongoCollection<Recipe> recipes,
        ILogger<SourceFilesController> logger)
    {
        _sourceFileService = sourceFileService;
        _recipes = recipes;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType<SourceFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SourceFileResponse>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        await using var stream = file.OpenReadStream();
        var sourceFile = await _sourceFileService.UploadAsync(file.FileName, file.ContentType, stream, cancellationToken);

        var response = ToResponse(sourceFile);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SourceFileResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SourceFileResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var sourceFiles = await _sourceFileService.GetAllAsync(cancellationToken);
        return Ok(sourceFiles.Select(ToResponse).ToList());
    }

    [HttpGet("{id}")]
    [ProducesResponseType<SourceFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SourceFileResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        var sourceFile = await _sourceFileService.GetByIdAsync(id, cancellationToken);

        if (sourceFile is null)
            return NotFound();

        return Ok(ToResponse(sourceFile));
    }

    [HttpGet("{id}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(string id, CancellationToken cancellationToken)
    {
        var sourceFile = await _sourceFileService.GetByIdAsync(id, cancellationToken);

        if (sourceFile is null)
            return NotFound();

        var filePath = _sourceFileService.GetStoredFilePath(sourceFile.StoredFileName);

        if (filePath is null)
            return NotFound();

        return PhysicalFile(filePath, sourceFile.ContentType, sourceFile.OriginalFileName);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _sourceFileService.DeleteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound();

        var update = Builders<Recipe>.Update
            .Set(r => r.SourceFileId, null)
            .Set(r => r.SourcePages, new List<int>());

        var result = await _recipes.UpdateManyAsync(r => r.SourceFileId == id, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
            _logger.LogInformation("Cleared source reference from {Count} recipe(s) after deleting source {Id}.", result.ModifiedCount, id);

        return NoContent();
    }

    private static SourceFileResponse ToResponse(SourceFile sourceFile)
    {
        return new SourceFileResponse(
            sourceFile.Id!,
            sourceFile.OriginalFileName,
            sourceFile.ContentType,
            sourceFile.UploadedAt);
    }
}
