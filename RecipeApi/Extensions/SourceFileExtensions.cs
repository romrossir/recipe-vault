using MongoDB.Driver;
using RecipeApi.Data;
using RecipeApi.Models;
using RecipeApi.Services;

namespace RecipeApi.Extensions;

public static class SourceFileExtensions
{
    public static IServiceCollection AddSourceFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("SourceFiles").Get<SourceFileSettings>()
            ?? throw new InvalidOperationException("SourceFiles settings are not configured.");

        var mongoSettings = configuration.GetSection("MongoDb").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDb settings are not configured.");

        var client = new MongoClient(mongoSettings.ConnectionString);
        var database = client.GetDatabase(mongoSettings.DatabaseName);
        var collection = database.GetCollection<SourceFile>(mongoSettings.SourceFilesCollectionName);

        services.AddSingleton(settings);
        services.AddSingleton(collection);
        services.AddScoped<ISourceFileService, SourceFileService>();

        return services;
    }
}
