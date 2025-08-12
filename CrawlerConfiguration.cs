namespace WebCrawler;

public class CrawlerConfiguration
{
    public required Uri TargetDomain { get; init; }
    public int FrontierCapacity { get; init; } = 1000;
    public int NumberOfConcurrentWorkers { get; init; } = 10;
}