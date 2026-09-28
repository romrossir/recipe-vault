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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();