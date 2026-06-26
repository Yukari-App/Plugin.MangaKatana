<div align="center">
  <h1>
    <img src="https://mangakatana.com/static/img/fav.png" alt="MangaKatana Logo" width="46" valign="middle"/> Yukari.Plugin.MangaKatana
  </h1>
</div>

<div align="center">
  
  ![GitHub Repo stars](https://img.shields.io/github/stars/Yukari-App/Plugin.MangaKatana?style=for-the-badge&color=3AB5E6)
  ![GitHub last commit](https://img.shields.io/github/last-commit/Yukari-App/Plugin.MangaKatana?style=for-the-badge&color=E6E6E6)
  ![GitHub repo size](https://img.shields.io/github/repo-size/Yukari-App/Plugin.MangaKatana?style=for-the-badge&color=3AB5E6)
</div>

<div align="center">
    <h2>📖 Overview</h2>

**Yukari.Plugin.MangaKatana** is a community plugin implementing the `IComicSource` interface from **[Yukari.Core](https://github.com/Yukari-App/Core)**.

It connects to the **[MangaKatana](https://mangakatana.com)** website and extracts data via web scraping.

Only **English** language is available.

Built for the **[Yukari](https://github.com/Yukari-App/Yukari)** Windows reader app.

</div>

<div align="center">
    <h2>📥 Installation</h2>
</div>

- Download the latest `.dll` from the [Releases](https://github.com/Yukari-App/Plugin.MangaKatana/releases) page.
- In Yukari, go to **Settings → Comic Sources → Add New Source** and select the downloaded file.
- For detailed instructions, see the [Yukari documentation](https://github.com/Yukari-App/Yukari#-comic-sources-installation).

<div align="center">
    <h2>🗒️ Notes</h2>
</div>

- No login required.
- **Filters**: Not supported by this source.
- **Languages**: Only English.
- **Performance**: Lazy static filters/languages, shared `HttpClient` with custom User-Agent.
- **Errors**: Returns empty results instead of crashing on API issues (e.g., invalid ID).
- **Page count**: The total number of pages isn't available until a chapter is opened. A `?` is shown in the chapter list until then.
- **Stack**: Use [HtmlAgilityPack](https://www.nuget.org/packages/HtmlAgilityPack/) for web scraping and [ILRepack.Lib.MSBuild.Task](https://www.nuget.org/packages/ILRepack.Lib.MSBuild.Task) to merge assemblies.

<div align="center">
    <h2>🤝 Contributing</h2>
  
Contributions are welcome! You can help improve **Yukari.Plugin.MangaKatana** in several ways:
</div>

- 🐛 **Report issues**: Found a bug or unexpected behavior? Open an [issue](../../issues) describing the problem.
- ✨ **Suggest features**: Have an idea to make **Yukari.Plugin.MangaKatana** better? Share it in the issues tab.
- 🔧 **Submit pull requests**: Fix bugs, improve code quality, or add new features.

<div align="center">
  <h2>📜 License</h2>

This project is licensed under the **GPL-3.0**. See the [LICENSE](LICENSE) file for details.

</div>
