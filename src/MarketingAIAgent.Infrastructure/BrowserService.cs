using System.Text.RegularExpressions;
using MarketingAIAgent.Domain;
using Microsoft.Playwright;

namespace MarketingAIAgent.Infrastructure;

public sealed class BrowserService(IAgentStore store, AppPaths paths) : IBrowserService
{
    readonly SemaphoreSlim gate = new(1, 1);
    IPlaywright? playwright;
    IBrowserContext? context;
    string? activeId;
    string? activePath;
    string? activeExecutable;
    async Task<IPage> OpenAsync(ChromeProfile profile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var settings = await store.SettingsAsync(ct);
        var profilePath = Path.GetFullPath(profile.DataPath);
        var defaultChrome = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"));
        if (profilePath.Equals(defaultChrome, StringComparison.OrdinalIgnoreCase) || profilePath.StartsWith(defaultChrome + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new AgentException("기본 Chrome 사용자 데이터 경로는 사용할 수 없습니다.");
        if (activeId != profile.Id || activePath != profilePath || activeExecutable != settings.ChromePath || context is null || context.Pages.Count == 0)
        {
            if (context is not null) await context.CloseAsync();
            context = null;
            playwright ??= await Playwright.CreateAsync();
            if (!string.IsNullOrWhiteSpace(settings.ChromePath) && !File.Exists(settings.ChromePath)) throw new AgentException("Chrome 실행 파일을 찾을 수 없습니다.");
            Directory.CreateDirectory(profilePath);
            context = await playwright.Chromium.LaunchPersistentContextAsync(profilePath, new()
            {
                Channel = string.IsNullOrWhiteSpace(settings.ChromePath) ? "chrome" : null,
                ExecutablePath = string.IsNullOrWhiteSpace(settings.ChromePath) ? null : settings.ChromePath,
                Headless = false, Locale = "ko-KR", ViewportSize = new() { Width = 1280, Height = 900 }, Timeout = 30000
            });
            context.SetDefaultTimeout(20000);
            activeId = profile.Id;
            activePath = profilePath;
            activeExecutable = settings.ChromePath;
        }
        return context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
    }
    static async Task<BrowserIdentity> IdentityAsync(IPage page)
    {
        var avatar = page.Locator("button#avatar-btn");
        if (!await avatar.IsVisibleAsync()) return new("로그인 필요", false);
        await avatar.ClickAsync();
        var handle = page.Locator("ytd-active-account-header-renderer #channel-handle");
        try
        {
            await handle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var name = (await handle.InnerTextAsync()).Trim();
            await page.Keyboard.PressAsync("Escape");
            return new(name, !string.IsNullOrWhiteSpace(name));
        }
        catch (PlaywrightException) { await page.Keyboard.PressAsync("Escape"); return new("계정 확인 실패", false); }
    }
    public async Task<BrowserIdentity> OpenProfileAsync(ChromeProfile profile, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var page = await OpenAsync(profile, ct);
            await page.GotoAsync("https://www.youtube.com/?hl=ko", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            var identity = await IdentityAsync(page);
            profile.Account = identity.Account; profile.LoginStatus = identity.LoggedIn ? "로그인 확인" : "Chrome에서 직접 로그인 후 다시 확인"; profile.LastUsedAt = DateTime.UtcNow;
            await store.SaveProfileAsync(profile, ct);
            return identity;
        }
        catch (PlaywrightException) { throw new AgentException("Chrome 실행/계정 확인 실패. Chrome 설치, 프로필 중복 사용 및 로그인 상태를 확인하세요."); }
        finally { gate.Release(); }
    }
    public async Task<BrowserResult> PostAsync(ChromeProfile profile, Video video, string text, Func<BrowserIdentity, Task<bool>> approve, Func<PostStatus, Task> progress, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        IPage? page = null;
        var clicked = false;
        try
        {
            page = await OpenAsync(profile, ct);
            // Closing the context interrupts selector waits on cancellation. No posting retry is performed.
            using var cancel = ct.Register(() => { if (context is not null) _ = context.CloseAsync(); });
            await page.GotoAsync(video.Url + "&hl=ko", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            var identity = await IdentityAsync(page);
            if (!identity.LoggedIn) throw new AgentException("로그인 계정을 확인하지 못했습니다. Chrome에서 직접 로그인하세요.");
            if (!string.Equals(profile.Account, identity.Account, StringComparison.Ordinal)) throw new AgentException("선택한 프로필의 계정이 변경되었습니다. 로그인 상태를 다시 확인하세요.");
            var comments = page.Locator("ytd-comments#comments");
            await comments.ScrollIntoViewIfNeededAsync();
            var placeholder = page.Locator("ytd-comment-simplebox-renderer #placeholder-area");
            await placeholder.ClickAsync();
            var editor = page.Locator("ytd-comment-simplebox-renderer #contenteditable-root[contenteditable='true']");
            await editor.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var ownMatchingThread = page.Locator("ytd-comment-thread-renderer").Filter(new() { Has = page.Locator("#content-text").Filter(new() { HasTextRegex = new Regex("^" + Regex.Escape(text) + "$") }) }).Filter(new() { Has = page.Locator("#author-text").Filter(new() { HasTextRegex = new Regex(@"^\s*" + Regex.Escape(identity.Account) + @"\s*$") }) });
            if (await ownMatchingThread.CountAsync() > 0) throw new AgentException("동일 계정의 같은 댓글이 이미 화면에 있습니다. 중복 게시하지 않습니다.");
            await progress(PostStatus.Typing);
            await editor.FillAsync("");
            await editor.PressSequentiallyAsync(text, new() { Delay = 30 });
            if ((await editor.InnerTextAsync()).Trim() != text.Trim() || Rules.ValidateComment(text) is not null) throw new AgentException("입력된 댓글과 승인 대상 댓글이 다릅니다.");
            ct.ThrowIfCancellationRequested();
            if (!await approve(identity)) return new(PostStatus.Failed, Error: "최종 승인을 취소했습니다. 게시하지 않았습니다.");
            ct.ThrowIfCancellationRequested();
            // Recheck account, URL and exact text after the approval dialog.
            var finalIdentity = await IdentityAsync(page);
            if (!finalIdentity.LoggedIn || finalIdentity.Account != identity.Account || Rules.ExtractVideoId(page.Url) != video.Id || (await editor.InnerTextAsync()).Trim() != text.Trim()) throw new AgentException("최종 검증에서 계정/영상/댓글 변경이 감지되었습니다.");
            var submit = page.Locator("ytd-comment-simplebox-renderer #submit-button").GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(댓글|Comment)$") });
            if (await submit.CountAsync() != 1 || !await submit.IsEnabledAsync()) throw new AgentException("게시 버튼을 안전하게 확인할 수 없습니다.");
            await progress(PostStatus.Posting);
            clicked = true; // Persisted Posting before click: any uncertain failure now blocks duplicates.
            await submit.ClickAsync();
            await progress(PostStatus.Verifying);
            var thread = ownMatchingThread.First;
            await thread.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            var href = await thread.Locator("a[href*='lc=']").First.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(href)) return new(PostStatus.VerificationUnknown, Error: "댓글은 보이지만 고유 링크를 확인하지 못했습니다.", Screenshot: await ScreenshotAsync(page));
            var url = new Uri(new Uri("https://www.youtube.com"), href).AbsoluteUri;
            if (new Uri(url).Host != "www.youtube.com" || Rules.ExtractVideoId(url) != video.Id) return new(PostStatus.VerificationUnknown, Error: "댓글 링크의 호스트 또는 영상 ID가 다릅니다.");
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.Locator("ytd-comments#comments").ScrollIntoViewIfNeededAsync();
            await ownMatchingThread.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            return new(PostStatus.Success, url);
        }
        catch (Exception ex) when (ex is PlaywrightException or AgentException or OperationCanceledException)
        {
            return new(clicked ? PostStatus.VerificationUnknown : PostStatus.Failed, Error: ex is AgentException ? ex.Message : ex is OperationCanceledException ? "취소됨" : "브라우저 작업 실패. 로그인/인증 요구, 화면 구조 변경, 프로필 충돌을 확인하세요.", Screenshot: page is null ? null : await ScreenshotAsync(page));
        }
        finally { gate.Release(); }
    }
    async Task<string?> ScreenshotAsync(IPage page)
    {
        try { var path = Path.Combine(paths.Screenshots, DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png"); await page.ScreenshotAsync(new() { Path = path, FullPage = true }); return path; }
        catch (PlaywrightException) { return null; }
    }
    public async ValueTask DisposeAsync() { if (context is not null) await context.CloseAsync(); playwright?.Dispose(); gate.Dispose(); }
}
