using RecipeApi.Extensions;
using RecipeApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddMongoDb(builder.Configuration);
builder.Services.AddOllama(builder.Configuration);
builder.Services.AddSourceFileStorage(builder.Configuration);
builder.Services.AddOcr(builder.Configuration);

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
    app.MapScalarApiReference();
    app.UseDeveloperExceptionPage();
    app.UseCors(policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();