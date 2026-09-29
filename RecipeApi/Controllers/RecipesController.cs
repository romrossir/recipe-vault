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

        var recipes = await _recipeService.GetAllAsync(
            skip,
            limit,
            cancellationToken);

        return Ok(recipes.Select(ToResponse));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RecipeResponse>> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        var recipe = await _recipeService.GetByIdAsync(
            id,
            cancellationToken);

        if (recipe is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(recipe));
    }

    [HttpPost]
    public async Task<ActionResult<RecipeResponse>> Create(
    CreateRecipeRequest request,
    CancellationToken cancellationToken)
    {
        var recipe = new Recipe
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

        recipe.SearchText = RecipeSearchTextBuilder.Build(recipe);

        recipe.Embedding = await _embeddingService.GenerateAsync(
            recipe.SearchText,
            cancellationToken);

        var createdRecipe = await _recipeService.CreateAsync(
            recipe,
            cancellationToken);

        var response = ToResponse(createdRecipe);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        UpdateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var recipe = new Recipe
        {
            Id = id,
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

        recipe.SearchText = RecipeSearchTextBuilder.Build(recipe);

        recipe.Embedding = await _embeddingService.GenerateAsync(
            recipe.SearchText,
            cancellationToken);

        var updated = await _recipeService.UpdateAsync(
            id,
            recipe,
            cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await _recipeService.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static RecipeResponse ToResponse(Recipe recipe)
    {
        return new RecipeResponse(
            recipe.Id!,
            recipe.Title,

            recipe.Ingredients
                .Select(i => new IngredientDto(
                    i.Name,
                    i.Quantity,
                    i.Unit))
                .ToList(),

            recipe.Steps.ToList());
    }

    [HttpPost("embedding-test")]
    public async Task<ActionResult> TestEmbedding(
    [FromBody] string text,
    CancellationToken cancellationToken)
    {
        var embedding = await _embeddingService.GenerateAsync(
            text,
            cancellationToken);

        return Ok(new
        {
            Dimensions = embedding.Length,
            FirstValues = embedding.Take(10)
        });
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

        var queryVector = await _embeddingService.GenerateAsync(
            q,
            cancellationToken);

        var results = await _recipeService.SearchAsync(
            queryVector,
            limit,
            cancellationToken);

        var response = results
            .Where(result =>
                !minScore.HasValue ||
                result.Score >= minScore.Value)
            .Select(result => new RecipeSearchResponse(
                result.Recipe.Id!,
                result.Recipe.Title,
                result.Recipe.Ingredients
                    .Select(i => new IngredientDto(
                        i.Name,
                        i.Quantity,
                        i.Unit))
                    .ToList(),
                result.Recipe.Steps,
                result.Score))
            .ToList();

        return Ok(response);
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AskRecipeResponse>> Ask(
        [FromBody] AskRecipeRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.GenerateAsync(
            request.Question,
            cancellationToken);

        var results = await _recipeService.SearchAsync(
            queryVector,
            5,
            cancellationToken);

        var context = string.Join(
            "\n\n--- RECIPE ---\n\n",
            results.Select(result => $"""
            Title: {result.Recipe.Title}

            Ingredients:
            {string.Join(
                    "\n",
                    result.Recipe.Ingredients.Select(i =>
                        $"- {i.Quantity} {i.Unit} {i.Name}".Trim()))}

            Steps:
            {string.Join("\n", result.Recipe.Steps)}
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

        var answer = await _llmService.GenerateAsync(
            systemPrompt,
            userMessage,
            cancellationToken);

        var recipes = results
            .Select(result => new RecipeSearchResponse(
                result.Recipe.Id!,
                result.Recipe.Title,
                result.Recipe.Ingredients
                    .Select(i => new IngredientDto(
                        i.Name,
                        i.Quantity,
                        i.Unit))
                    .ToList(),
                result.Recipe.Steps,
                result.Score))
            .ToList();

        return Ok(new AskRecipeResponse(
            answer,
            recipes));
    }
}