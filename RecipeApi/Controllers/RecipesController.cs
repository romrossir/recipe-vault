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

    // GET: api/recipes
    [HttpGet]
    public ActionResult<IEnumerable<RecipeResponse>> GetAll()
    {
        var recipes = _recipeService
            .GetAll()
            .Select(ToResponse);

        return Ok(recipes);
    }

    // GET: api/recipes/1
    [HttpGet("{id:int}")]
    public ActionResult<RecipeResponse> GetById(int id)
    {
        var recipe = _recipeService.GetById(id);

        if (recipe is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(recipe));
    }

    // POST: api/recipes
    [HttpPost]
    public ActionResult<RecipeResponse> Create(
        CreateRecipeRequest request)
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

        var createdRecipe = _recipeService.Create(recipe);

        var response = ToResponse(createdRecipe);

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    // PUT: api/recipes/1
    [HttpPut("{id:int}")]
    public IActionResult Update(
        int id,
        UpdateRecipeRequest request)
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

        var updated = _recipeService.Update(id, recipe);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: api/recipes/1
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var deleted = _recipeService.Delete(id);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static RecipeResponse ToResponse(Recipe recipe)
    {
        return new RecipeResponse(
            recipe.Id,
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