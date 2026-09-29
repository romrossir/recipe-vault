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
    private readonly IEmbeddingService _embeddingService;
    private readonly ILlmService _llmService;

    public RecipesController(
        IRecipeService recipeService,
        IEmbeddingService embeddingService,
        ILlmService llmService)
    {
        _recipeService = recipeService;
        _embeddingService = embeddingService;
        _llmService = llmService;
    }

    [HttpGet]
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

        return Ok(recipes.Select(ToResponse));
    }

    [HttpGet("{id}")]
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
    public async Task<ActionResult<RecipeResponse>> Create(SaveRecipeRequest request, CancellationToken cancellationToken)
    {
        var recipe = ToRecipe(request);
        var createdRecipe = await _recipeService.CreateAsync(recipe, cancellationToken);
        var response = ToResponse(createdRecipe);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
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

        var queryVector = await _embeddingService.GenerateAsync(q, cancellationToken);
        var results = await _recipeService.SearchAsync(queryVector, limit, cancellationToken);

        var response = results
            .Where(r =>
                !minScore.HasValue ||
                r.Score >= minScore.Value)
            .Select(r => ToSearchResponse(r))
            .ToList();

        return Ok(response);
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AskRecipeResponse>> Ask(
        [FromBody] AskRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.GenerateAsync(request.Question, cancellationToken);
        var results = await _recipeService.SearchAsync(queryVector, 5, cancellationToken);

        var context = string.Join(
            "\n\n--- RECIPE ---\n\n",
            results.Select(r => $"""
            Title: {r.Recipe.Title}

            Ingredients:
            {string.Join(
                    "\n",
                    r.Recipe.Ingredients.Select(i =>
                        $"- {i.Quantity} {i.Unit} {i.Name}".Trim()))}

            Steps:
            {string.Join("\n", r.Recipe.Steps)}
            """));

        var systemPrompt = """
        Tu es un assistant spécialisé dans les recettes de cuisine.

        Réponds à la question uniquement à partir des recettes
        présentes dans le contexte.

        Ne crée pas d'ingrédient, de recette ou d'information
        qui n'est pas présente dans le contexte.

        Si le contexte ne permet pas de répondre à la question,
        indique-le clairement.
        """;

        var userMessage = $"""
        Question :
        {request.Question}

        Contexte :
        {context}
        """;

        var answer = await _llmService.GenerateAsync(systemPrompt, userMessage, cancellationToken);

        var recipes = results
            .Select(r => ToSearchResponse(r))
            .ToList();

        return Ok(new AskRecipeResponse(
            answer,
            recipes));
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