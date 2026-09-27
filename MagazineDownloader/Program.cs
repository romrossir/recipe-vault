using Microsoft.Playwright;
using System.Text.RegularExpressions;

var url = args.Length > 0
    ? args[0]
    : throw new ArgumentException("Usage: dotnet run -- <viewer-url>");

var outputDirectory = Path.Combine(
    Directory.GetCurrentDirectory(),
    "downloads");

Directory.CreateDirectory(outputDirectory);

// -----------------------------------------------------------------------------
// State
// -----------------------------------------------------------------------------

var chunksByPdfUrl =
    new Dictionary<string, Dictionary<long, byte[]>>();

var totalSizeByPdfUrl =
    new Dictionary<string, long>();

var outputFileByPdfUrl =
    new Dictionary<string, string>();

var titleByPdfUrl =
    new Dictionary<string, string>();

var contentIdByPdfUrl =
    new Dictionary<string, string?>();

var savedByPdfUrl =
    new HashSet<string>();

var stateLock = new object();

// -----------------------------------------------------------------------------
// Playwright
// -----------------------------------------------------------------------------

using var playwright = await Playwright.CreateAsync();

await using var browser = await playwright.Chromium.LaunchAsync(
    new BrowserTypeLaunchOptions
    {
        Headless = false
    });

var context = await browser.NewContextAsync();

var page = await context.NewPageAsync();

// -----------------------------------------------------------------------------
// PDF interception
// -----------------------------------------------------------------------------

await page.RouteAsync(
    "**/proxy.php*",
    async route =>
    {
        var request = route.Request;
        var requestUrl = request.Url;

        if (!requestUrl.Contains("target_file=p_pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            await route.ContinueAsync();
            return;
        }

        try
        {
            var response = await route.FetchAsync();

            var status = response.Status;

            // We only care about PDF responses.
            if (status != 200 && status != 206)
            {
                await route.FulfillAsync(
                    new RouteFulfillOptions
                    {
                        Response = response
                    });

                return;
            }

            var body = await response.BodyAsync();

            // -----------------------------------------------------------------
            // Get information about the current magazine.
            //
            // IMPORTANT:
            // Do not hold stateLock while awaiting Playwright calls.
            // -----------------------------------------------------------------

            bool known;

            lock (stateLock)
            {
                known = chunksByPdfUrl.ContainsKey(requestUrl);
            }

            if (!known)
            {
                var magazineInfo =
                    await GetMagazineInfoAsync(page);

                lock (stateLock)
                {
                    // Another concurrent request may have initialized it.
                    if (!chunksByPdfUrl.ContainsKey(requestUrl))
                    {
                        chunksByPdfUrl[requestUrl] =
                            new Dictionary<long, byte[]>();

                        titleByPdfUrl[requestUrl] =
                            magazineInfo.Title;

                        contentIdByPdfUrl[requestUrl] =
                            magazineInfo.ContentId;

                        outputFileByPdfUrl[requestUrl] =
                            CreateOutputFile(
                                outputDirectory,
                                magazineInfo.Title,
                                magazineInfo.ContentId);

                        Console.WriteLine();
                        Console.WriteLine("========================================");
                        Console.WriteLine("New magazine detected");
                        Console.WriteLine(
                            $"Title     : {magazineInfo.Title}");
                        Console.WriteLine(
                            $"ContentId : {magazineInfo.ContentId ?? "(unknown)"}");
                        Console.WriteLine(
                            $"Output    : {outputFileByPdfUrl[requestUrl]}");
                        Console.WriteLine("========================================");
                    }
                }
            }

            // -----------------------------------------------------------------
            // Handle Content-Range
            // -----------------------------------------------------------------

            var contentRange =
                response.Headers.TryGetValue(
                    "content-range",
                    out var contentRangeHeader)
                    ? contentRangeHeader
                    : null;

            if (TryParseContentRange(
                    contentRange,
                    out var start,
                    out var end,
                    out var totalSize))
            {
                bool complete;

                lock (stateLock)
                {
                    var chunks = chunksByPdfUrl[requestUrl];

                    chunks[start] = body;

                    totalSizeByPdfUrl[requestUrl] = totalSize;

                    var received =
                        chunks.Values.Sum(
                            chunk => (long)chunk.Length);

                    Console.WriteLine(
                        $"PDF chunk: {start:N0}-{end:N0} " +
                        $"({body.Length:N0} bytes) | " +
                        $"{received:N0} / {totalSize:N0}");

                    complete =
                        !savedByPdfUrl.Contains(requestUrl) &&
                        received >= totalSize &&
                        HasCompleteFile(
                            chunks,
                            totalSize);
                }

                if (complete)
                {
                    string outputFile;

                    lock (stateLock)
                    {
                        outputFile =
                            outputFileByPdfUrl[requestUrl];

                        savedByPdfUrl.Add(requestUrl);
                    }

                    try
                    {
                        Dictionary<long, byte[]> chunksCopy;

                        lock (stateLock)
                        {
                            chunksCopy =
                                new Dictionary<long, byte[]>(
                                    chunksByPdfUrl[requestUrl]);
                        }

                        await SavePdfAsync(
                            outputFile,
                            chunksCopy,
                            totalSize);

                        Console.WriteLine();
                        Console.WriteLine("****************************************");
                        Console.WriteLine("PDF SAVED");
                        Console.WriteLine($"File: {outputFile}");
                        Console.WriteLine($"Size: {totalSize:N0} bytes");
                        Console.WriteLine("****************************************");
                        Console.WriteLine();
                    }
                    catch
                    {
                        lock (stateLock)
                        {
                            savedByPdfUrl.Remove(requestUrl);
                        }

                        throw;
                    }
                }
            }

            // Let the browser receive the response normally.
            await route.FulfillAsync(
                new RouteFulfillOptions
                {
                    Response = response
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("PDF interception error:");
            Console.WriteLine(ex);

            try
            {
                await route.ContinueAsync();
            }
            catch
            {
                // The browser may already have gone away.
            }
        }
    });

// -----------------------------------------------------------------------------
// Navigate
// -----------------------------------------------------------------------------

Console.WriteLine("Opening viewer...");

await page.GotoAsync(
    url,
    new PageGotoOptions
    {
        WaitUntil = WaitUntilState.DOMContentLoaded
    });

Console.WriteLine("Viewer loaded.");
Console.WriteLine();
Console.WriteLine("Navigate/open magazines in the browser.");
Console.WriteLine("Each detected magazine will be downloaded automatically.");
Console.WriteLine();
Console.WriteLine($"Output directory: {outputDirectory}");
Console.WriteLine();
Console.WriteLine("Press ENTER to stop.");
Console.WriteLine();

Console.ReadLine();

Console.WriteLine("Stopping...");

// -----------------------------------------------------------------------------
// Local functions
// -----------------------------------------------------------------------------

async Task<(string Title, string? ContentId)> GetMagazineInfoAsync(
    IPage currentPage)
{
    string? title = null;

    // 1. Browser document title
    try
    {
        title = await currentPage.TitleAsync();
    }
    catch
    {
        // Ignore.
    }

    // 2. OpenGraph title
    if (string.IsNullOrWhiteSpace(title))
    {
        try
        {
            var locator =
                currentPage.Locator(
                    "meta[property='og:title']");

            if (await locator.CountAsync() > 0)
            {
                title =
                    await locator.First.GetAttributeAsync("content");
            }
        }
        catch
        {
            // Ignore.
        }
    }

    // 3. First H1
    if (string.IsNullOrWhiteSpace(title))
    {
        try
        {
            var locator =
                currentPage.Locator("h1");

            if (await locator.CountAsync() > 0)
            {
                title =
                    await locator.First.TextContentAsync();
            }
        }
        catch
        {
            // Ignore.
        }
    }

    title = CleanMagazineTitle(title);

    // -------------------------------------------------------------------------
    // contentId from viewer URL
    // -------------------------------------------------------------------------

    string? contentId = null;

    try
    {
        var uri = new Uri(currentPage.Url);

        var query =
            ParseQueryString(uri.Query);

        query.TryGetValue(
            "contentId",
            out contentId);
    }
    catch
    {
        // Ignore.
    }

    return (
        string.IsNullOrWhiteSpace(title)
            ? "Magazine"
            : title,
        contentId);
}

string CleanMagazineTitle(string? title)
{
    if (string.IsNullOrWhiteSpace(title))
        return "Magazine";

    title = title.Trim();

    // Remove common viewer suffixes.
    title = Regex.Replace(
        title,
        @"\s*[-|]\s*(Mozzo|PublishingCenter).*$",
        "",
        RegexOptions.IgnoreCase);

    title = Regex.Replace(
        title,
        @"\s+",
        " ");

    foreach (var invalidChar in Path.GetInvalidFileNameChars())
    {
        title = title.Replace(
            invalidChar,
            '_');
    }

    return title.Trim(
        ' ',
        '.');
}

string CreateOutputFile(
    string directory,
    string title,
    string? contentId)
{
    var baseName = title;

    // If the title doesn't already contain an issue number,
    // add contentId to make it easier to identify.
    if (!string.IsNullOrWhiteSpace(contentId) &&
        !Regex.IsMatch(
            baseName,
            @"\b(?:N[°º.]?|No\.?|Issue)\s*\d+\b",
            RegexOptions.IgnoreCase))
    {
        baseName += $" - {contentId}";
    }

    baseName = CleanMagazineTitle(baseName);

    var file =
        Path.Combine(
            directory,
            $"{baseName}.pdf");

    var index = 2;

    while (File.Exists(file))
    {
        file =
            Path.Combine(
                directory,
                $"{baseName} ({index}).pdf");

        index++;
    }

    return file;
}

bool TryParseContentRange(
    string? value,
    out long start,
    out long end,
    out long total)
{
    start = 0;
    end = 0;
    total = 0;

    if (string.IsNullOrWhiteSpace(value))
        return false;

    // Example:
    // bytes 0-655359/17995936

    var match =
        Regex.Match(
            value,
            @"bytes\s+(\d+)-(\d+)/(\d+)",
            RegexOptions.IgnoreCase);

    if (!match.Success)
        return false;

    return
        long.TryParse(
            match.Groups[1].Value,
            out start)
        &&
        long.TryParse(
            match.Groups[2].Value,
            out end)
        &&
        long.TryParse(
            match.Groups[3].Value,
            out total);
}

bool HasCompleteFile(
    Dictionary<long, byte[]> chunks,
    long totalSize)
{
    if (chunks.Count == 0)
        return false;

    var expectedStart = 0L;

    foreach (var chunk in chunks.OrderBy(x => x.Key))
    {
        var start = chunk.Key;
        var length = chunk.Value.LongLength;

        if (start != expectedStart)
            return false;

        expectedStart += length;
    }

    return expectedStart == totalSize;
}

async Task SavePdfAsync(
    string outputFile,
    Dictionary<long, byte[]> chunks,
    long totalSize)
{
    Console.WriteLine();
    Console.WriteLine(
        $"Reconstructing PDF: {outputFile}");

    // IMPORTANT:
    // Close the write stream before opening the file again for verification.
    await using (var stream = File.Create(outputFile))
    {
        foreach (var chunk in chunks.OrderBy(x => x.Key))
        {
            await stream.WriteAsync(
                chunk.Value);
        }

        await stream.FlushAsync();
    }

    var fileInfo =
        new FileInfo(outputFile);

    if (fileInfo.Length != totalSize)
    {
        throw new InvalidOperationException(
            $"Invalid PDF size. " +
            $"Expected {totalSize}, " +
            $"got {fileInfo.Length}.");
    }

    await using var verifyStream =
        File.OpenRead(outputFile);

    var header = new byte[5];

    var read =
        await verifyStream.ReadAsync(header);

    var headerText =
        System.Text.Encoding.ASCII.GetString(
            header,
            0,
            read);

    if (!headerText.StartsWith("%PDF-"))
    {
        throw new InvalidOperationException(
            "Downloaded file does not appear to be a PDF.");
    }
}

Dictionary<string, string> ParseQueryString(
    string query)
{
    var result =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

    if (query.StartsWith("?"))
        query = query[1..];

    foreach (var part in query.Split(
                 '&',
                 StringSplitOptions.RemoveEmptyEntries))
    {
        var pieces =
            part.Split(
                '=',
                2);

        var key =
            Uri.UnescapeDataString(
                pieces[0]);

        var value =
            pieces.Length > 1
                ? Uri.UnescapeDataString(
                    pieces[1])
                : "";

        result[key] = value;
    }

    return result;
}