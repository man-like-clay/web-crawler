using System.Net;

namespace WebCrawler.Utils;

public interface IHttpAccessor
{
    Task<string> GetHtmlAsync(Uri url, CancellationToken ct);
}

public sealed class HttpAccessor : IHttpAccessor
{
    private readonly HttpClient _http;

    public HttpAccessor(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MyCrawler/1.0");
        }

        if (_http.DefaultRequestHeaders.Accept.Count == 0)
        {
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        }

        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public Task<string> GetHtmlAsync(Uri url, CancellationToken ct) => _http.GetStringAsync(url, ct);
}