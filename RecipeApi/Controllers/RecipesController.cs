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

    public RecipesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecipeResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var recipes = await _recipeService.GetAllAsync(
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
}