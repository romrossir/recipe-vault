using MongoDB.Driver;
using RecipeApi.Data;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Extensions;

public static class MongoDbExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("MongoDb").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDb settings are not configured.");

        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        var recipesCollection = database.GetCollection<Recipe>(settings.RecipesCollectionName);

        services.AddSingleton(settings);
        services.AddSingleton(recipesCollection);
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<RecipeImporter>();

        return services;
    }
}
