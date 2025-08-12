using WebCrawler.Utils;
using Microsoft.Extensions.Logging;

namespace WebCrawler;

class Program
{
    static async Task Main(string[] args)
    {
        var seedUrl = await ParseArguments(args);

        if (seedUrl == null)
        {
            await Console.Error.WriteLineAsync("Invalid URL provided. Usage: dotnet run -- <startUrl>");
            return;
        }

        Console.WriteLine($"[crawler] start={seedUrl}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var crawler = SetUpCrawler(seedUrl);

        try
        {
            // Start the crawler
            await crawler.Run(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Handle Ctrl+C gracefully
            Console.WriteLine("[crawler] cancelled");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[crawler] error: {ex.Message}");
        }
    }

    private static CrawlerService SetUpCrawler(Uri seedUrl)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .AddSimpleConsole()
            .SetMinimumLevel(LogLevel.Information)
        );

        var options = new CrawlerConfiguration
        {
            TargetDomain = seedUrl,
            NumberOfConcurrentWorkers = 10,
            FrontierCapacity = 1000
        };

        var httpClient = new HttpClient();
        var httpAccessor = new HttpAccessor(httpClient);
        var logger = loggerFactory.CreateLogger<CrawlerService>();

        return new CrawlerService(httpAccessor, options, logger);
    }

    private static async Task<Uri?> ParseArguments(string[] args)
    {
        if (args.Length == 1 && Uri.TryCreate(args[0], UriKind.Absolute, out var seedUrl))
        {
            return seedUrl;
        }

        await Console.Error.WriteLineAsync("Usage: dotnet run -- <startUrl>");
        return null;
    }
}