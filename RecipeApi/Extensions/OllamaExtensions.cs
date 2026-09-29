using RecipeApi.Data;
using RecipeApi.Services;

namespace RecipeApi.Extensions;

public static class OllamaExtensions
{
    public static IServiceCollection AddOllama(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Ollama").Get<OllamaSettings>()
            ?? throw new InvalidOperationException("Ollama settings are not configured.");

        services.AddSingleton(settings);
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(
            client => client.BaseAddress = new Uri(settings.BaseUrl));
        services.AddHttpClient<ILlmService, OllamaLlmService>(
            client =>
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            });

        return services;
    }
}
