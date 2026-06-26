using HtmlAgilityPack;
using Yukari.Core.Models;
using Yukari.Core.Sources;

namespace Yukari.Plugin.MangaKatana;

[ComicSourceMetadata(
    "MangaKatana",
    "0.1.0-Alpha+core2.3.0",
    "https://github.com/Yukari-App/Plugin.MangaKatana/releases",
    "https://mangakatana.com/static/img/fav.png",
    "Read manga from MangaKatana, a simple and fast manga reader."
)]
public class MangaKatanaSource : IComicSource
{
    private static IReadOnlyList<Filter>? _filters;
    private static IReadOnlyDictionary<string, string>? _languages;

    public IReadOnlyList<Filter> Filters => _filters ??= [];

    public IReadOnlyDictionary<string, string> Languages =>
        _languages ??= new Dictionary<string, string> { { "en", "English" } };

    private const string BaseUrl = "https://mangakatana.com";

    private static readonly HttpClient _httpClient = new HttpClient();

    static MangaKatanaSource()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Yukari.Plugin.MangaKatana/0.1.0");
    }

    public Task<IReadOnlyList<Comic>> SearchAsync(
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Comic>> GetTrendingAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<Comic?> GetDetailsAsync(string comicId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Chapter>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ChapterPage>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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
            Slug: id,
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
                using var response = await _httpClient.GetAsync(url, ct);

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
            catch (Exception ex)
                when (attempt < maxRetries
                    && (ex is HttpRequestException || ex is TaskCanceledException)
                )
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
            var match = Regex.Match(content, @"url=(.+)", RegexOptions.IgnoreCase);
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

    private bool IsValidHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
        return content.Length >= 50;
    }

    private ComicStatus GetComicStatus(string? status) =>
        status switch
        {
            "Ongoing" => ComicStatus.Ongoing,
            "Completed" => ComicStatus.Completed,
            "Hiatus" => ComicStatus.Hiatus,
            "Canceled" => ComicStatus.Cancelled,
            _ => ComicStatus.Unknown,
        };

    private string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var match = Regex.Match(url, @"/manga/([^/]+)$");
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
