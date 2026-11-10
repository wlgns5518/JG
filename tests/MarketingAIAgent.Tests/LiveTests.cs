using MarketingAIAgent.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace MarketingAIAgent.Tests;
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute() { if (Environment.GetEnvironmentVariable("MARKETING_AGENT_LIVE_TESTS") != "1") Skip = "실제 API/Chrome 검증은 MARKETING_AGENT_LIVE_TESTS=1 및 Windows DPAPI 키 설정 필요"; }
}
public sealed class LiveTests
{
    [LiveFact]
    public async Task YouTubeReadOnlyLiveCall()
    {
        var paths = new AppPaths();
        var service = new YouTubeService(new HttpClient(), new DpapiSecretStore(paths));
        var page = await service.SearchVideosAsync("연애 고민", "date", null, default);
        Assert.NotNull(page.Videos);
    }
    [LiveFact]
    public async Task GeminiReadOnlyLiveCall()
    {
        var paths = new AppPaths();
        var store = new SqliteAgentStore("Data Source=" + paths.Database, paths);
        await store.InitializeAsync(default);
        var youtube = new YouTubeService(new HttpClient(), new DpapiSecretStore(paths));
        var page = await youtube.SearchVideosAsync("연애 고민", "date", null, default);
        var video = Assert.Single(page.Videos.Take(1));
        var comments = await youtube.GetTopCommentsAsync(video.Id, default);
        var output = await new GeminiAnalysisService(new HttpClient(), new DpapiSecretStore(paths), store).AnalyzeVideoAsync(video, comments, default);
        Assert.Equal(video.Id, output.Draft.VideoId);
    }
    [LiveFact]
    public async Task ChromePersistentContextAndKoreanInputLocalFixture()
    {
        using var playwright = await Playwright.CreateAsync();
        var profile = Path.Combine(Path.GetTempPath(), "marketing-chrome-test-" + Guid.NewGuid());
        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(profile, new() { Channel = "chrome", Headless = false });
        var page = await context.NewPageAsync();
        await page.SetContentAsync("<html lang='ko'><body><div contenteditable='true' role='textbox' aria-label='댓글'></div><button>댓글</button></body></html>");
        var editor = page.GetByRole(AriaRole.Textbox, new() { Name = "댓글" });
        await editor.PressSequentiallyAsync(RulesTests.ValidComment);
        Assert.Equal(RulesTests.ValidComment, await editor.InnerTextAsync());
        // Local fixture only: never posts a comment to YouTube.
    }
}
