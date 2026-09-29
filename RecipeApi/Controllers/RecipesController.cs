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
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(
        IRecipeService recipeService,
        IRecipeAssistantService assistantService,
        ILogger<RecipesController> logger)
    {
        _recipeService = recipeService;
        _assistantService = assistantService;
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

    private static Recipe ToRecipe(SaveRecipeRequest request)
    {
        return new Recipe
        {
            Title = request.Title,

            Ingredients = request.Ingredients
                .Select(i => new Ingredient
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                })
                .ToList(),

            Steps = request.Steps.ToList()
        };
    }

    private static RecipeResponse ToResponse(Recipe recipe)
    {
        return new RecipeResponse(
            recipe.Id!,
            recipe.Title,
            ToIngredientDtos(recipe.Ingredients),
            recipe.Steps.ToList());
    }

    private static RecipeSearchResponse ToSearchResponse(RecipeSearchResult result)
    {
        return new RecipeSearchResponse(
            result.Recipe.Id!,
            result.Recipe.Title,
            ToIngredientDtos(result.Recipe.Ingredients),
            result.Recipe.Steps,
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