using Microsoft.AspNetCore.Mvc;
using RecipeApi.Dtos;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;
    private readonly IRecipeAssistantService _assistantService;
    private readonly IRecipeAgentService _agentService;
    private readonly ISourceFileService _sourceFileService;
    private readonly IOcrClient _ocrClient;
    private readonly IRecipeExtractorService _extractorService;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(
        IRecipeService recipeService,
        IRecipeAssistantService assistantService,
        IRecipeAgentService agentService,
        ISourceFileService sourceFileService,
        IOcrClient ocrClient,
        IRecipeExtractorService extractorService,
        ILogger<RecipesController> logger)
    {
        _recipeService = recipeService;
        _assistantService = assistantService;
        _agentService = agentService;
        _sourceFileService = sourceFileService;
        _ocrClient = ocrClient;
        _extractorService = extractorService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<IEnumerable<RecipeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RecipeResponse>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            return BadRequest("Skip must be non-negative.");
        }

        if (limit is < 1 or > 100)
        {
            return BadRequest("Limit must be between 1 and 100.");
        }

        var recipes = await _recipeService.GetAllAsync(skip, limit, cancellationToken);
        _logger.LogInformation("Returning {Count} recipes (skip={Skip}, limit={Limit}).", recipes.Count, skip, limit);

        return Ok(recipes.Select(ToResponse));
    }

    [HttpGet("{id}")]
    [ProducesResponseType<RecipeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        var recipe = await _recipeService.GetByIdAsync(id, cancellationToken);

        if (recipe is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(recipe));
    }

    [HttpPost]
    [ProducesResponseType<RecipeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeResponse>> Create(SaveRecipeRequest request, CancellationToken cancellationToken)
    {
        var recipe = ToRecipe(request);
        var createdRecipe = await _recipeService.CreateAsync(recipe, cancellationToken);
        var response = ToResponse(createdRecipe);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, SaveRecipeRequest request, CancellationToken cancellationToken)
    {
        var recipe = ToRecipe(request);
        var updated = await _recipeService.UpdateAsync(id, recipe, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var deleted = await _recipeService.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<RecipeSearchResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RecipeSearchResponse>>> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 5,
        [FromQuery] double? minScore = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query cannot be empty.");
        }

        if (limit is < 1 or > 50)
        {
            return BadRequest("Limit must be between 1 and 50.");
        }

        var results = await _recipeService.SearchAsync(q, limit, cancellationToken);
        _logger.LogInformation("Search for '{Query}' returned {Count} results.", q, results.Count);

        var response = results
            .Where(r =>
                !minScore.HasValue ||
                r.Score >= minScore.Value)
            .Select(r => ToSearchResponse(r))
            .ToList();

        return Ok(response);
    }

    [HttpPost("ask")]
    [ProducesResponseType<AskRecipeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AskRecipeResponse>> Ask(
        [FromBody] AskRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ask: '{Question}'", request.Question);
        var result = await _assistantService.AskAsync(request.Question, cancellationToken);

        var recipes = result.Sources
            .Select(r => ToSearchResponse(r))
            .ToList();

        return Ok(new AskRecipeResponse(result.Answer, recipes));
    }

    [HttpPost("agent")]
    [ProducesResponseType<AgentQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentQueryResponse>> Agent(
        [FromBody] AgentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query cannot be empty.");

        _logger.LogInformation("Agent: '{Query}'", request.Query);
        var result = await _agentService.QueryAsync(request.Query, cancellationToken);

        var recipes = result.Results
            .Select(r => ToSearchResponse(r))
            .ToList();

        return Ok(new AgentQueryResponse(result.Answer, recipes));
    }

    [HttpPost("agent/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task AgentStream(
        [FromBody] AgentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        _logger.LogInformation("Agent stream: '{Query}'", request.Query);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        await foreach (var evt in _agentService.QueryStreamAsync(request.Query, cancellationToken))
        {
            var (eventType, data) = evt switch
            {
                Models.ToolCallStreamEvent tc => ("tool_call", System.Text.Json.JsonSerializer.Serialize(new { tool = tc.ToolName }, jsonOptions)),
                Models.SearchResultsStreamEvent sr => ("search_results", System.Text.Json.JsonSerializer.Serialize(new { count = sr.Count }, jsonOptions)),
                Models.DeltaStreamEvent d => ("delta", System.Text.Json.JsonSerializer.Serialize(new { text = d.Text }, jsonOptions)),
                Models.FinalResultsStreamEvent fr => ("results", System.Text.Json.JsonSerializer.Serialize(
                    fr.Results.Select(r => ToSearchResponse(r)), jsonOptions)),
                Models.DoneStreamEvent => ("done", "{}"),
                _ => (null, null)
            };

            if (eventType is null) continue;

            await Response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("ingest")]
    [ProducesResponseType<IReadOnlyList<RecipeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RecipeResponse>>> Ingest(
        IFormFile file,
        [FromQuery] string mode = "vision",
        [FromQuery] int? page = null,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (mode is not "vision" and not "ocr")
            return BadRequest("Mode must be 'vision' or 'ocr'.");

        await using var uploadStream = file.OpenReadStream();
        var sourceFile = await _sourceFileService.UploadAsync(file.FileName, file.ContentType, uploadStream, cancellationToken);

        IReadOnlyList<Recipe> recipes;
        if (mode == "vision")
        {
            await using var imageStream = file.OpenReadStream();
            recipes = await _extractorService.ExtractFromImageAsync(imageStream, cancellationToken);
        }
        else
        {
            await using var ocrStream = file.OpenReadStream();
            var ocrText = await _ocrClient.ExtractTextAsync(file.FileName, file.ContentType, ocrStream, cancellationToken);
            recipes = await _extractorService.ExtractFromTextAsync(ocrText, cancellationToken);
        }

        var responses = new List<RecipeResponse>();
        foreach (var recipe in recipes)
        {
            recipe.SourceFileId = sourceFile.Id;
            if (page.HasValue)
                recipe.SourcePages = [page.Value];

            var created = await _recipeService.CreateAsync(recipe, cancellationToken);
            _logger.LogInformation("Ingested recipe '{Title}' from source {SourceFileId} (mode={Mode}).", created.Title, sourceFile.Id, mode);
            responses.Add(ToResponse(created));
        }

        return Ok(responses);
    }

    [HttpPost("ingest/manual")]
    [ProducesResponseType<IReadOnlyList<RecipeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RecipeResponse>>> IngestManual(
        IFormFile file,
        [FromForm] string recipes,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        List<SaveRecipeRequest>? parsed;
        try
        {
            parsed = System.Text.Json.JsonSerializer.Deserialize<List<SaveRecipeRequest>>(recipes,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("Invalid JSON in recipes field.");
        }

        if (parsed is null || parsed.Count == 0)
            return BadRequest("At least one recipe is required.");

        await using var uploadStream = file.OpenReadStream();
        var sourceFile = await _sourceFileService.UploadAsync(file.FileName, file.ContentType, uploadStream, cancellationToken);

        var responses = new List<RecipeResponse>();
        foreach (var request in parsed)
        {
            var recipe = ToRecipe(request);
            recipe.SourceFileId = sourceFile.Id;
            if (request.SourcePages.Count > 0)
                recipe.SourcePages = request.SourcePages.ToList();

            var created = await _recipeService.CreateAsync(recipe, cancellationToken);
            _logger.LogInformation("Ingested recipe '{Title}' from source {SourceFileId} (manual).", created.Title, sourceFile.Id);
            responses.Add(ToResponse(created));
        }

        return Ok(responses);
    }

    private static Recipe ToRecipe(SaveRecipeRequest request)
    {
        return new Recipe
        {
            Title = request.Title,
            Author = request.Author,
            PrepTime = request.PrepTime,
            CookTime = request.CookTime,
            Servings = request.Servings,

            Ingredients = request.Ingredients
                .Select(i => new Ingredient
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                })
                .ToList(),

            Tags = request.Tags.ToList(),
            SourceFileId = request.SourceFileId,
            SourcePages = request.SourcePages.ToList()
        };
    }

    private static RecipeResponse ToResponse(Recipe recipe)
    {
        return new RecipeResponse(
            recipe.Id!,
            recipe.Title,
            recipe.Author,
            recipe.PrepTime,
            recipe.CookTime,
            recipe.Servings,
            ToIngredientDtos(recipe.Ingredients),
            recipe.Tags.ToList(),
            recipe.SourceFileId,
            recipe.SourcePages);
    }

    private static RecipeSearchResponse ToSearchResponse(RecipeSearchResult result)
    {
        return new RecipeSearchResponse(
            result.Recipe.Id!,
            result.Recipe.Title,
            result.Recipe.Author,
            result.Recipe.PrepTime,
            result.Recipe.CookTime,
            result.Recipe.Servings,
            ToIngredientDtos(result.Recipe.Ingredients),
            result.Recipe.Tags,
            result.Recipe.SourceFileId,
            result.Recipe.SourcePages,
            result.Score);
    }

    private static List<IngredientDto> ToIngredientDtos(IEnumerable<Ingredient> ingredients)
    {
        return ingredients
            .Select(i => new IngredientDto(
                i.Name,
                i.Quantity,
                i.Unit))
            .ToList();
    }
}