using RecipeApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register framework services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register application services
builder.Services.AddScoped<IRecipeService, RecipeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
