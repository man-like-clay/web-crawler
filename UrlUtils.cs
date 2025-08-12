namespace WebCrawler.Utils;

public static class UrlUtils
{
    public static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static Uri Normalise(Uri uri)
    {
        var uriBuilder = new UriBuilder(uri) { Fragment = "" };

        if (uriBuilder is { Scheme: "http", Port: 80 } or { Scheme: "https", Port: 443 })
        {
            uriBuilder.Port = -1;
        }

        var path = string.IsNullOrEmpty(uriBuilder.Path) ? "/" : uriBuilder.Path;
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        uriBuilder.Path = path;

        return uriBuilder.Uri;
    }

    public static bool IsUrlInDomain(Uri url, Uri? targetDomain)
    {
        if (string.IsNullOrWhiteSpace(url.AbsoluteUri) || targetDomain == null)
        {
            return false;
        }

        try
        {
            return url.Host.Equals(targetDomain.Host, StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }
}