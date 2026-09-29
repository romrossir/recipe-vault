using MongoDB.Driver;
using RecipeApi.Models;
using RecipeApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var mongoConnectionString =
    builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDb:ConnectionString is not configured.");

var mongoDatabaseName =
    builder.Configuration["MongoDb:DatabaseName"]
    ?? throw new InvalidOperationException(
        "MongoDb:DatabaseName is not configured.");

var mongoCollectionName =
    builder.Configuration["MongoDb:RecipesCollectionName"]
    ?? throw new InvalidOperationException(
        "MongoDb:RecipesCollectionName is not configured.");

var mongoClient = new MongoClient(mongoConnectionString);

var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

var recipesCollection =
    mongoDatabase.GetCollection<Recipe>(mongoCollectionName);

builder.Services.AddSingleton(recipesCollection);

builder.Services.AddScoped<IRecipeService, RecipeService>();

builder.Services.AddScoped<RecipeImporter>();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
    });

builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(
    client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

if (args.Length == 2 && args[0].Equals("import", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();

    var importer = scope.ServiceProvider
        .GetRequiredService<RecipeImporter>();

    await importer.ImportAsync(args[1]);

    return;
}

app.Run();