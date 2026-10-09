using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RecipeApi.Controllers;
using RecipeApi.Dtos;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Tests.Controllers;

public sealed class RecipesControllerTests
{
    private readonly IRecipeService _recipeService = Substitute.For<IRecipeService>();
    private readonly IRecipeAssistantService _assistantService = Substitute.For<IRecipeAssistantService>();
    private readonly IRecipeAgentService _agentService = Substitute.For<IRecipeAgentService>();
    private readonly ISourceFileService _sourceFileService = Substitute.For<ISourceFileService>();
    private readonly IOcrClient _ocrClient = Substitute.For<IOcrClient>();
    private readonly IRecipeExtractorService _extractorService = Substitute.For<IRecipeExtractorService>();
    private readonly RecipesController _sut;

    public RecipesControllerTests()
    {
        _sut = new RecipesController(
            _recipeService,
            _assistantService,
            _agentService,
            _sourceFileService,
            _ocrClient,
            _extractorService,
            NullLogger<RecipesController>.Instance);
    }

    // -- GetAll --

    [Fact]
    public async Task GetAll_ReturnsRecipes()
    {
        var recipes = new List<Recipe> { CreateRecipe("1", "Crêpes") };
        _recipeService.GetAllAsync(0, 20, Arg.Any<CancellationToken>()).Returns(recipes);

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Single(items);
        Assert.Equal("Crêpes", items[0].Title);
    }

    [Fact]
    public async Task GetAll_NegativeSkip_ReturnsBadRequest()
    {
        var result = await _sut.GetAll(skip: -1);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_LimitTooHigh_ReturnsBadRequest()
    {
        var result = await _sut.GetAll(limit: 101);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_LimitZero_ReturnsBadRequest()
    {
        var result = await _sut.GetAll(limit: 0);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // -- GetById --

    [Fact]
    public async Task GetById_ExistingId_ReturnsRecipe()
    {
        var recipe = CreateRecipe("1", "Tarte Tatin");
        _recipeService.GetByIdAsync("1", Arg.Any<CancellationToken>()).Returns(recipe);

        var result = await _sut.GetById("1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RecipeResponse>(ok.Value);
        Assert.Equal("Tarte Tatin", response.Title);
    }

    [Fact]
    public async Task GetById_NonExistingId_ReturnsNotFound()
    {
        _recipeService.GetByIdAsync("999", Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var result = await _sut.GetById("999", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // -- Create --

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        var request = new SaveRecipeRequest { Title = "Crêpes" };
        _recipeService.CreateAsync(Arg.Any<Recipe>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var r = ci.Arg<Recipe>();
                r.Id = "new-id";
                return r;
            });

        var result = await _sut.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        var response = Assert.IsType<RecipeResponse>(created.Value);
        Assert.Equal("new-id", response.Id);
        Assert.Equal("Crêpes", response.Title);
    }

    [Fact]
    public async Task Create_WithSourceFile_MapsSourceFields()
    {
        var request = new SaveRecipeRequest
        {
            Title = "Tarte",
            SourceFileId = "sf-1",
            SourcePages = [3, 4]
        };
        _recipeService.CreateAsync(Arg.Any<Recipe>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var r = ci.Arg<Recipe>();
                r.Id = "new-id";
                return r;
            });

        var result = await _sut.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<RecipeResponse>(created.Value);
        Assert.Equal("sf-1", response.SourceFileId);
        Assert.Equal([3, 4], response.SourcePages);
    }

    // -- Update --

    [Fact]
    public async Task Update_ExistingId_ReturnsNoContent()
    {
        _recipeService.UpdateAsync("1", Arg.Any<Recipe>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.Update("1", new SaveRecipeRequest { Title = "Updated" }, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Update_NonExistingId_ReturnsNotFound()
    {
        _recipeService.UpdateAsync("999", Arg.Any<Recipe>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Update("999", new SaveRecipeRequest { Title = "X" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // -- Delete --

    [Fact]
    public async Task Delete_ExistingId_ReturnsNoContent()
    {
        _recipeService.DeleteAsync("1", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.Delete("1", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_NonExistingId_ReturnsNotFound()
    {
        _recipeService.DeleteAsync("999", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Delete("999", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // -- Search --

    [Fact]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        var result = await _sut.Search(q: "", limit: 5);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Search_LimitTooHigh_ReturnsBadRequest()
    {
        var result = await _sut.Search(q: "test", limit: 51);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Search_ReturnsResults()
    {
        var results = new List<RecipeSearchResult>
        {
            new(CreateRecipe("1", "Cheesecake"), 0.95)
        };
        _recipeService.SearchAsync("cheesecake", 5, Arg.Any<CancellationToken>()).Returns(results);

        var result = await _sut.Search(q: "cheesecake", limit: 5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<RecipeSearchResponse>>(ok.Value).ToList();
        Assert.Single(items);
        Assert.Equal("Cheesecake", items[0].Title);
    }

    [Fact]
    public async Task Search_MinScoreFiltersResults()
    {
        var results = new List<RecipeSearchResult>
        {
            new(CreateRecipe("1", "High"), 0.95),
            new(CreateRecipe("2", "Low"), 0.50)
        };
        _recipeService.SearchAsync("test", 5, Arg.Any<CancellationToken>()).Returns(results);

        var result = await _sut.Search(q: "test", limit: 5, minScore: 0.80);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IEnumerable<RecipeSearchResponse>>(ok.Value).ToList();
        Assert.Single(items);
        Assert.Equal("High", items[0].Title);
    }

    // -- Ask --

    [Fact]
    public async Task Ask_ReturnsAnswerAndRecipes()
    {
        var assistantResult = new RecipeAssistantResult(
            "Voici la recette.",
            new List<RecipeSearchResult> { new(CreateRecipe("1", "Cheesecake"), 0.90) });

        _assistantService.AskAsync("cheesecake", Arg.Any<CancellationToken>()).Returns(assistantResult);

        var result = await _sut.Ask(new AskRecipeRequest { Question = "cheesecake" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AskRecipeResponse>(ok.Value);
        Assert.Equal("Voici la recette.", response.Answer);
        Assert.Single(response.Recipes);
    }

    // -- Agent --

    [Fact]
    public async Task Agent_ReturnsAnswerAndResults()
    {
        var agentResult = new RecipeAgentResult(
            "Voici des recettes au chocolat.",
            new List<RecipeSearchResult> { new(CreateRecipe("1", "Fondant"), 0.92) });

        _agentService.QueryAsync("chocolat", Arg.Any<CancellationToken>()).Returns(agentResult);

        var result = await _sut.Agent(new AgentQueryRequest { Query = "chocolat" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AgentQueryResponse>(ok.Value);
        Assert.Equal("Voici des recettes au chocolat.", response.Answer);
        Assert.Single(response.Results);
        Assert.Equal("Fondant", response.Results[0].Title);
    }

    [Fact]
    public async Task Agent_EmptyQuery_ReturnsBadRequest()
    {
        var result = await _sut.Agent(new AgentQueryRequest { Query = "" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // -- Ingest --

    [Fact]
    public async Task Ingest_VisionMode_ReturnsRecipes()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");
        SetupSourceFileUpload("sf-1", "photo.jpg", "abc.jpg", "image/jpeg");

        _extractorService.ExtractFromImageAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe> { new() { Title = "Crêpes" }, new() { Title = "Gaufres" } });

        SetupRecipeCreate();

        var result = await _sut.Ingest(file, mode: "vision");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var responses = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Equal(2, responses.Count);
        Assert.Equal("Crêpes", responses[0].Title);
        Assert.Equal("Gaufres", responses[1].Title);
        Assert.All(responses, r => Assert.Equal("sf-1", r.SourceFileId));
    }

    [Fact]
    public async Task Ingest_OcrMode_ReturnsRecipes()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");
        SetupSourceFileUpload("sf-1", "photo.jpg", "abc.jpg", "image/jpeg");

        _ocrClient.ExtractTextAsync("photo.jpg", "image/jpeg", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("Crêpes: farine, oeufs, lait");

        _extractorService.ExtractFromTextAsync("Crêpes: farine, oeufs, lait", Arg.Any<CancellationToken>())
            .Returns(new List<Recipe> { new() { Title = "Crêpes" } });

        SetupRecipeCreate();

        var result = await _sut.Ingest(file, mode: "ocr");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var responses = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Single(responses);
        Assert.Equal("Crêpes", responses[0].Title);
    }

    [Fact]
    public async Task Ingest_WithPage_SetsSourcePages()
    {
        var file = CreateFormFile("scan.pdf", "application/pdf", "data");
        SetupSourceFileUpload("sf-2", "scan.pdf", "def.pdf", "application/pdf");

        _extractorService.ExtractFromImageAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe> { new() { Title = "Tarte" } });

        SetupRecipeCreate();

        var result = await _sut.Ingest(file, page: 5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var responses = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Single(responses);
        Assert.Equal([5], responses[0].SourcePages);
    }

    [Fact]
    public async Task Ingest_NoRecipesExtracted_ReturnsEmptyList()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");
        SetupSourceFileUpload("sf-1", "photo.jpg", "abc.jpg", "image/jpeg");

        _extractorService.ExtractFromImageAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new List<Recipe>());

        var result = await _sut.Ingest(file, mode: "vision");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var responses = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Empty(responses);
    }

    [Fact]
    public async Task Ingest_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("empty.jpg", "image/jpeg", "");

        var result = await _sut.Ingest(file);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Ingest_InvalidMode_ReturnsBadRequest()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");

        var result = await _sut.Ingest(file, mode: "invalid");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // -- IngestManual --

    [Fact]
    public async Task IngestManual_CreatesRecipesFromJson()
    {
        var file = CreateFormFile("magazine.pdf", "application/pdf", "data");
        SetupSourceFileUpload("sf-1", "magazine.pdf", "abc.pdf", "application/pdf");
        SetupRecipeCreate();

        var recipesJson = """
            [
              { "title": "Crêpes", "ingredients": [], "tags": ["dessert"], "sourcePages": [3] },
              { "title": "Gaufres", "ingredients": [], "tags": ["dessert"], "sourcePages": [5] }
            ]
            """;

        var result = await _sut.IngestManual(file, recipesJson);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var responses = Assert.IsAssignableFrom<IEnumerable<RecipeResponse>>(ok.Value).ToList();
        Assert.Equal(2, responses.Count);
        Assert.Equal("Crêpes", responses[0].Title);
        Assert.Equal("sf-1", responses[0].SourceFileId);
        Assert.Equal([3], responses[0].SourcePages);
        Assert.Equal("Gaufres", responses[1].Title);
        Assert.Equal([5], responses[1].SourcePages);
    }

    [Fact]
    public async Task IngestManual_InvalidJson_ReturnsBadRequest()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");

        var result = await _sut.IngestManual(file, "not-json");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task IngestManual_EmptyArray_ReturnsBadRequest()
    {
        var file = CreateFormFile("photo.jpg", "image/jpeg", "data");

        var result = await _sut.IngestManual(file, "[]");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task IngestManual_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("empty.jpg", "image/jpeg", "");

        var result = await _sut.IngestManual(file, """[{"title":"X"}]""");

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private void SetupSourceFileUpload(string id, string originalName, string storedName, string contentType)
    {
        var sourceFile = new Models.SourceFile
        {
            Id = id,
            OriginalFileName = originalName,
            StoredFileName = storedName,
            ContentType = contentType
        };
        _sourceFileService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(sourceFile);
    }

    private void SetupRecipeCreate()
    {
        _recipeService.CreateAsync(Arg.Any<Recipe>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var r = ci.Arg<Recipe>();
                r.Id = "new-id";
                return r;
            });
    }

    private static Recipe CreateRecipe(string id, string title)
    {
        return new Recipe { Id = id, Title = title };
    }

    private static Microsoft.AspNetCore.Http.IFormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var file = Substitute.For<Microsoft.AspNetCore.Http.IFormFile>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(bytes.Length);
        file.OpenReadStream().Returns(_ => new MemoryStream(bytes));
        return file;
    }
}
