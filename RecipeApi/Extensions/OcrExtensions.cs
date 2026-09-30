using RecipeApi.Data;
using RecipeApi.Services;

namespace RecipeApi.Extensions;

public static class OcrExtensions
{
    public static IServiceCollection AddOcr(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Ocr").Get<OcrSettings>()
            ?? throw new InvalidOperationException("Ocr settings are not configured.");

        services.AddSingleton(settings);
        services.AddHttpClient<IOcrClient, OcrClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        return services;
    }
}
