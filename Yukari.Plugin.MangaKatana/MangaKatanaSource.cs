using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Yukari.Core.Models;
using Yukari.Core.Sources;
using Yukari.Core.Sources.Http;

namespace Yukari.Plugin.MangaKatana;

[ComicSourceMetadata(
    "MangaKatana",
    "1.2.0+core2.5.0",
    "https://github.com/Yukari-App/Plugin.MangaKatana/releases",
    "https://mangakatana.com/static/img/fav.png",
    "Read manga from MangaKatana, a simple and fast manga reader."
)]
public partial class MangaKatanaSource : IComicSource, IRequiresHttpClient
{
    private const string SourceLanguage = "en";
    private const string BaseUrl = "https://mangakatana.com";

    private ISharedHttpClient? _httpClient;

    public IReadOnlyList<Filter> Filters => _filters;
    public IReadOnlyDictionary<string, string> Languages => _languages;

    public void SetHttpClient(ISharedHttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<Comic>> SearchAsync(
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var queryParams = new Dictionary<string, string[]>
        {
            ["search"] = [query],
            ["search_by"] = ["m_name"],
        };

        string searchUrl = $"{BaseUrl}/page/{page}?{ToQueryString(queryParams)}";
        return await FetchComicListAsync(searchUrl, ct);
    }

    public async Task<IReadOnlyList<Comic>> GetTrendingAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        string trendingUrl = $"{BaseUrl}/latest/page/{page}";
        return await FetchComicListAsync(trendingUrl, ct);
    }

    public async Task<Comic?> GetDetailsAsync(string comicId, CancellationToken ct = default)
    {
        var detailsUrl = $"{BaseUrl}/manga/{comicId}";

        var html = await GetHTMLAsync(detailsUrl, ct);
        if (html == null)
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'heading')]");
        string title = titleNode?.InnerText.Trim() ?? "Unknown Title";

        var summaryNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'summary')]/p");
        string? description = summaryNode?.InnerText.Trim();

        var coverImgNode = doc.DocumentNode.SelectSingleNode(
            "//div[contains(@class, 'cover')]//img"
        );
        string? coverUrl = coverImgNode?.GetAttributeValue("src", null!);

        var authorNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'authors')]//a");
        string[] authors =
            authorNodes?.Select(a => a.InnerText.Trim()).ToArray() ?? Array.Empty<string>();
        string? author = authors.FirstOrDefault();

        var genreNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'genres')]//a");
        string[] tags =
            genreNodes?.Select(g => g.InnerText.Trim()).ToArray() ?? Array.Empty<string>();

        var statusNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'status')]");
        string? statusStr = statusNode?.InnerText.Trim();
        ComicStatus status = GetComicStatus(statusStr);

        return new Comic(
            Id: comicId,
            ComicUrl: detailsUrl,
            Title: title,
            Author: author,
            Description: description,
            Tags: tags,
            Year: null,
            CoverImageUrl: coverUrl,
            Langs: [SourceLanguage],
            Status: status
        );
    }

    public async Task<IReadOnlyList<Chapter>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        var chaptersUrl = $"{BaseUrl}/manga/{comicId}";

        var html = await GetHTMLAsync(chaptersUrl, ct);
        if (html == null)
            return Array.Empty<Chapter>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var chapterRows = doc.DocumentNode.SelectNodes(
            "//div[contains(@class, 'chapters')]//table//tbody//tr"
        );
        if (chapterRows is not { Count: > 0 })
            return Array.Empty<Chapter>();

        var chapters = new List<Chapter>();

        // MangaKatana returns chapters in descending order (newest first).
        // Reverse to get chronological order (oldest first).
        foreach (var row in chapterRows.Reverse())
        {
            var linkNode = row.SelectSingleNode(".//td[1]//a");
            if (linkNode == null)
                continue;

            string href = linkNode.GetAttributeValue("href", "");
            string? chapterId = ExtractChapterIdFromUrl(href);
            if (string.IsNullOrEmpty(chapterId))
                continue;

            string title = linkNode.InnerText.Trim();

            var timeNode = row.SelectSingleNode(".//td[2]//div[contains(@class, 'update_time')]");
            string? dateStr = timeNode?.InnerText.Trim();

            DateOnly? lastUpdate = null;
            if (!string.IsNullOrEmpty(dateStr))
                if (DateTime.TryParse(dateStr, out DateTime dt))
                    lastUpdate = DateOnly.FromDateTime(dt);

            var chapter = new Chapter(
                Id: chapterId,
                Title: title,
                Number: null,
                Volume: null,
                Language: language,
                Groups: [],
                LastUpdate: lastUpdate,
                Pages: null
            );

            chapters.Add(chapter);
        }

        return chapters;
    }

    public async Task<IReadOnlyList<ChapterPage>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        var pagesUrl = $"{BaseUrl}/manga/{comicId}/{chapterId}";

        var html = await GetHTMLAsync(pagesUrl, ct);
        if (html == null)
            return Array.Empty<ChapterPage>();

        var match = Regex.Match(html, @"var thzq\s*=\s*\[(.*?)\];", RegexOptions.Singleline);
        if (!match.Success)
            return Array.Empty<ChapterPage>();

        var urlMatches = Regex.Matches(match.Groups[1].Value, @"'([^']+)'");
        if (urlMatches.Count == 0)
            return Array.Empty<ChapterPage>();

        var pages = new List<ChapterPage>();
        for (int i = 0; i < urlMatches.Count; i++)
        {
            string imageUrl = urlMatches[i].Groups[1].Value;
            pages.Add(new ChapterPage(Number: i + 1, ImageUrl: imageUrl));
        }

        return pages;
    }

    public async Task<byte[]?> GetImageBytesAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.Add("Referer", BaseUrl);

        using var response = await _httpClient!.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException(
                "MangaKatana Rate Limit Exceeded. Try again later.",
                null,
                HttpStatusCode.TooManyRequests
            );

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
