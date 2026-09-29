using MongoDB.Driver;
using RecipeApi.Data;
using RecipeApi.Models;
using RecipeApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var mongoSettings = builder.Configuration.GetSection("MongoDb").Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDb settings are not configured.");
var mongoClient = new MongoClient(mongoSettings.ConnectionString);
var mongoDatabase = mongoClient.GetDatabase(mongoSettings.DatabaseName);
var recipesCollection = mongoDatabase.GetCollection<Recipe>(mongoSettings.RecipesCollectionName);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(recipesCollection);
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<RecipeImporter>();

var ollamaSettings = builder.Configuration.GetSection("Ollama").Get<OllamaSettings>()
    ?? throw new InvalidOperationException("Ollama settings are not configured.");

builder.Services.AddSingleton(ollamaSettings);
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(
    client => client.BaseAddress = new Uri(ollamaSettings.BaseUrl));
builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(
    client => client.BaseAddress = new Uri(ollamaSettings.BaseUrl));

var app = builder.Build();

if (args.Length == 2 && args[0].Equals("import", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var importer = scope.ServiceProvider.GetRequiredService<RecipeImporter>();
    await importer.ImportAsync(args[1]);

    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();