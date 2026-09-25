using RecipeExtractor.Models;
using RecipeExtractor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IOcrClient, OcrClient>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:8080");
    });

builder.Services.AddHttpClient<IRecipeExtractor, OllamaRecipeExtractor>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
         client.Timeout = TimeSpan.FromMinutes(5);
    });

var app = builder.Build();

// app.MapPost("/recipes/extract",
//     async (
//         ExtractRecipeRequest request,
//         IRecipeExtractor recipeExtractor,
//         CancellationToken cancellationToken) =>
//     {
//         if (string.IsNullOrWhiteSpace(request.Text))
//         {
//             return Results.BadRequest(new
//             {
//                 error = "Text is required."
//             });
//         }

//         var recipe = await recipeExtractor.ExtractAsync(
//             request.Text,
//             cancellationToken);

//         return Results.Ok(recipe);
//     });

app.MapPost("/recipes/extract",
    async (
        IFormFile image,
        IOcrClient ocrClient,
        IRecipeExtractor recipeExtractor,
        CancellationToken cancellationToken) =>
    {
        if (image.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = "Image is required."
            });
        }

        var text = await ocrClient.ExtractTextAsync(
            image,
            cancellationToken);

        var recipe = await recipeExtractor.ExtractAsync(
            text,
            cancellationToken);

        return Results.Ok(recipe);
    })
    .DisableAntiforgery();

app.Run();