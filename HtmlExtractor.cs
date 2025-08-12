using HtmlAgilityPack;

namespace WebCrawler.Utils;

public static class HtmlExtractor
{
    public static IEnumerable<Uri> ExtractUrls(Uri baseUri, string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        var anchors = GetAnchors(htmlDocument);

        if (anchors == null)
        {
            yield break;
        }

        foreach (var htmlNode in anchors)
        {
            var href = htmlNode.GetAttributeValue("href", null);
            if (IsSkippableHref(href))
            {
                continue;
            }

            if (!TryResolveHref(baseUri, href, out var resolved))
            {
                continue;
            }

            if (!UrlUtils.IsHttpScheme(resolved))
            {
                continue;
            }

            yield return UrlUtils.Normalise(resolved);
        }
    }

    private static HtmlNodeCollection? GetAnchors(HtmlDocument doc)
    {
        return doc.DocumentNode.SelectNodes("//a[@href]");
    }

    private static bool IsSkippableHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return true;

        href = href.Trim().Trim('\'', '"');

        return href.StartsWith('#')
               || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
               || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
               || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
               || href.StartsWith("sms:", StringComparison.OrdinalIgnoreCase)
               || href.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveHref(Uri baseUri, string? href, out Uri resolved)
    {
        resolved = null!;
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        href = href.Trim().Trim('\'', '"');

        if (href.StartsWith("//"))
        {
            href = $"{baseUri.Scheme}:{href}";
        }

        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(href, UriKind.Absolute, out resolved);
        }

        return Uri.TryCreate(baseUri, href, out resolved);
    }
}