using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Yukari.Core.Models;

namespace Yukari.Plugin.MangaKatana;

public partial class MangaKatanaSource
{
    private static readonly IReadOnlyList<Filter> _filters = [];

    private static readonly IReadOnlyDictionary<string, string> _languages = new Dictionary<
        string,
        string
    >
    {
        { "en", "English" },
    };

    private async Task<IReadOnlyList<Comic>> FetchComicListAsync(string url, CancellationToken ct)
    {
        var html = await GetHTMLWithRedirectHandlingAsync(url, ct);
        if (html == null)
            return Array.Empty<Comic>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var bookList = doc.DocumentNode.SelectSingleNode("//div[@id='book_list']");
        if (bookList == null)
        {
            var comic = ParseSeriesPage(doc);
            return comic != null ? new List<Comic> { comic } : Array.Empty<Comic>();
        }

        var items = doc.DocumentNode.SelectNodes(
            "//div[@id='book_list']//div[contains(@class, 'item')]"
        );
        if (items is not { Count: > 0 })
            return Array.Empty<Comic>();

        var comics = new List<Comic>();

        foreach (var item in items)
        {
            var linkNode = item.SelectSingleNode(".//div[contains(@class, 'media')]//a");
            if (linkNode == null)
                continue;

            string href = linkNode.GetAttributeValue("href", "");
            string? id = ExtractIdFromUrl(href);
            if (string.IsNullOrEmpty(id))
                continue;

            var titleNode = item.SelectSingleNode(
                ".//div[contains(@class, 'text')]//h3[contains(@class, 'title')]//a"
            );
            string title = titleNode?.InnerText.Trim() ?? "Unknown Title";

            var imgNode = item.SelectSingleNode(".//div[contains(@class, 'media')]//img");
            string? coverUrl = imgNode?.GetAttributeValue("src", null!);

            var comic = new Comic(
                Id: id,
                ComicUrl: null,
                Title: title,
                Author: null,
                Description: null,
                Tags: [],
                Year: null,
                CoverImageUrl: coverUrl,
                Langs: []
            );

            comics.Add(comic);
        }

        return comics;
    }

    private Comic? ParseSeriesPage(HtmlDocument doc)
    {
        var ogUrlNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:url']");
        string? url = ogUrlNode?.GetAttributeValue("content", "");
        string? id = ExtractIdFromUrl(url);

        if (string.IsNullOrEmpty(id))
            return null;

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'heading')]");
        string title = titleNode?.InnerText.Trim() ?? "Unknown Title";

        var coverImgNode = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class, 'cover')]//img"
        );
        string? coverUrl = coverImgNode?.GetAttributeValue("src", null!);

        return new Comic(
            Id: id,
            ComicUrl: null,
            Title: title,
            Author: null,
            Description: null,
            Tags: [],
            Year: null,
            CoverImageUrl: coverUrl,
            Langs: []
        );
    }

    private async Task<string?> GetHTMLAsync(string url, CancellationToken ct = default)
    {
        const int maxRetries = 3;
        int attempt = 0;
        while (attempt < maxRetries)
        {
            attempt++;
            try
            {
                using var response = await _httpClient!.SendAsync(new(HttpMethod.Get, url), ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new HttpRequestException(
                        "MangaKatana Rate Limit Exceeded. Try again later.",
                        null,
                        HttpStatusCode.TooManyRequests
                    );

                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                    return default;

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!IsValidHtml(content))
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000 * attempt, ct);
                        continue;
                    }
                    return null;
                }

                return content;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException))
            {
                await Task.Delay(1000 * attempt, ct);
                continue;
            }
            catch
            {
                if (attempt == maxRetries)
                    throw;
                await Task.Delay(1000 * attempt, ct);
            }
        }
        return null;
    }

    private async Task<string?> GetHTMLWithRedirectHandlingAsync(
        string url,
        CancellationToken ct = default
    )
    {
        var html = await GetHTMLAsync(url, ct);
        if (string.IsNullOrEmpty(html))
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var metaRefresh = doc.DocumentNode.SelectSingleNode("//meta[@http-equiv='refresh']");
        if (metaRefresh != null)
        {
            var content = metaRefresh.GetAttributeValue("content", "");
            var match = Regex.Match(content.Trim('\'', '"'), @"url=(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var redirectUrl = match.Groups[1].Value.Trim();
                if (!redirectUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    redirectUrl = BaseUrl + redirectUrl;
                return await GetHTMLAsync(redirectUrl, ct);
            }
        }
        return html;
    }

    private static bool IsValidHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
        return content.Length >= 50;
    }

    private static ComicStatus GetComicStatus(string? status) =>
        status switch
        {
            "Ongoing" => ComicStatus.Ongoing,
            "Completed" => ComicStatus.Completed,
            "Hiatus" => ComicStatus.Hiatus,
            "Canceled" => ComicStatus.Cancelled,
            _ => ComicStatus.Unknown,
        };

    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var match = Regex.Match(url, @"/manga/([^/]+)$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractChapterIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var match = Regex.Match(url.TrimEnd('/'), @"/([^/]+)$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ToQueryString(Dictionary<string, string[]> source) =>
        string.Join(
            "&",
            source.SelectMany(kvp =>
                kvp.Value.Select(v => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v)}")
            )
        );
}
