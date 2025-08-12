using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using WebCrawler.Utils;
using Microsoft.Extensions.Logging;

namespace WebCrawler;

public class CrawlerService
{
    private readonly IHttpAccessor _httpAccessor;
    private readonly CrawlerConfiguration _crawlerConfiguration;
    private readonly ILogger<CrawlerService> _logger;
    private readonly TokenBucketRateLimiter _rateLimiter;

    private readonly Channel<Uri> _frontier;
    private readonly ConcurrentDictionary<Uri, byte> _seenUrls = new();

    private int _hasSignalledCompletion; // 0 = not signalled, 1 = signalled
    private int _pendingUrls;

    public CrawlerService(
        IHttpAccessor httpAccessor,
        CrawlerConfiguration crawlerConfiguration,
        ILogger<CrawlerService> logger)
    {
        _httpAccessor = httpAccessor;
        _crawlerConfiguration = crawlerConfiguration;
        _logger = logger;

        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 6,
            TokensPerPeriod = 6,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = crawlerConfiguration.NumberOfConcurrentWorkers,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        
        var channelOptions = new BoundedChannelOptions(crawlerConfiguration.FrontierCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _frontier = Channel.CreateBounded<Uri>(channelOptions);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        await EnqueueIfNewAsync(_crawlerConfiguration.TargetDomain, cancellationToken);

        var tasks = Enumerable.Range(0, _crawlerConfiguration.NumberOfConcurrentWorkers)
            .Select(_ => Worker(cancellationToken)).ToArray();

        await Task.WhenAll(tasks);
    }

    private async ValueTask EnqueueIfNewAsync(Uri url, CancellationToken ct)
    {
        if (Volatile.Read(ref _hasSignalledCompletion) == 1)
        {
            return;
        }

        if (!UrlUtils.IsUrlInDomain(url, _crawlerConfiguration.TargetDomain))
        {
            return;
        }

        if (!_seenUrls.TryAdd(url, 0))
        {
            return;
        }

        Interlocked.Increment(ref _pendingUrls);
        try
        {
            while (await _frontier.Writer.WaitToWriteAsync(ct))
            {
                if (_frontier.Writer.TryWrite(url)) return;
            }

            Interlocked.Decrement(ref _pendingUrls);
        }
        catch
        {
            Interlocked.Decrement(ref _pendingUrls);
            _logger.LogError("Failed to enqueue URL: {Item}", url);
            throw;
        }
    }

    private async Task Worker(CancellationToken ct)
    {
        try
        {
            while (await _frontier.Reader.WaitToReadAsync(ct))
            {
                while (_frontier.Reader.TryRead(out var url))
                {
                    using var lease = await _rateLimiter.AcquireAsync(1, ct);
                    try
                    {
                        var html = await _httpAccessor.GetHtmlAsync(url, ct);
                        foreach (var link in HtmlExtractor.ExtractUrls(url, html))
                        {
                            await EnqueueIfNewAsync(link, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error fetching {Item}: {ExceptionMessage}", url, ex.Message);
                    }

                    _logger.LogInformation("URL processed: {Item}", url);

                    if (Interlocked.Decrement(ref _pendingUrls) == 0)
                    {
                        if (Interlocked.Exchange(ref _hasSignalledCompletion, 1) == 0)
                        {
                            _frontier.Writer.TryComplete();
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker cancelled: {CurrentId}", Task.CurrentId);
        }
    }
}