using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "OCR service is running");

app.MapPost("/ocr", async (IFormFile image) =>
{
    if (image.Length == 0)
    {
        return Results.BadRequest("Empty image");
    }

    var tempFile = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid() + Path.GetExtension(image.FileName));

    try
    {
        // Save uploaded image temporarily
        await using (var stream = File.Create(tempFile))
        {
            await image.CopyToAsync(stream);
        }

        // Run Tesseract
        var startInfo = new ProcessStartInfo
        {
            FileName = "tesseract",
            Arguments = $"\"{tempFile}\" stdout -l fra --psm 12",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var text = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            return Results.Problem(
                $"Tesseract failed: {error}");
        }

        return Results.Ok(new
        {
            text = text.Trim()
        });
    }
    finally
    {
        // Always remove temporary file
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }
})
.DisableAntiforgery();

app.Run();