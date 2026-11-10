using System.Globalization;
using System.Text.RegularExpressions;

namespace MarketingAIAgent.Domain;

public sealed class Video
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Thumbnail { get; set; } = "";
    public DateTime PublishedAt { get; set; }
    public long Views { get; set; }
    public string Url => $"https://www.youtube.com/watch?v={Id}";
    public string Summary => $"{Channel}  ·  {Views:N0}회  ·  {PublishedAt:yyyy-MM-dd}";
}
public sealed class ViewSnapshot
{
    public long Id { get; set; }
    public string VideoId { get; set; } = "";
    public long Views { get; set; }
    public DateTime CapturedAt { get; set; }
}
public sealed class VideoAnalysis
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VideoId { get; set; } = "";
    public string MainConcern { get; set; } = "";
    public string EvidenceJson { get; set; } = "[]";
    public string CommentsJson { get; set; } = "[]";
    public string SourceType { get; set; } = "metadata";
    public string SourceNotice { get; set; } = "";
    public string Model { get; set; } = "";
    public double Confidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class CommentDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VideoId { get; set; } = "";
    public string AnalysisId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class ChromeProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string DataPath { get; set; } = "";
    public string LoginStatus { get; set; } = "미확인";
    public string Account { get; set; } = "";
    public DateTime? LastUsedAt { get; set; }
}
public enum PostStatus { Pending, AwaitingApproval, OpeningBrowser, Typing, Posting, Verifying, Success, Failed, VerificationUnknown }
public sealed class CommentPost
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string VideoId { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string Account { get; set; } = "";
    public string CommentText { get; set; } = "";
    public PostStatus Status { get; set; } = PostStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SeoulDate { get; set; } = "";
    public DateTime? PostedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ScreenshotPath { get; set; }
    public string? CommentUrl { get; set; }
    public string? GuardVideoId { get; set; }
}
public sealed class ActivityLog
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = "";
}
public sealed class AppSetting { public string Id { get; set; } = "settings"; public string Json { get; set; } = "{}"; }
public sealed class AgentSettings
{
    public string GeminiModel { get; set; } = "gemini-3.1-flash-lite";
    public string ChromePath { get; set; } = "";
    public int DailyLimit { get; set; } = 10;
    public string DatabasePath { get; set; } = "";
    public string LogPath { get; set; } = "";
    public string ProfileRoot { get; set; } = "";
}
public record SearchPage(IReadOnlyList<Video> Videos, string? NextPageToken);
public record CommentSources(IReadOnlyList<string> Comments, string Notice);
public record AnalysisOutput(VideoAnalysis Analysis, CommentDraft Draft);
public record BrowserResult(PostStatus Status, string? Url = null, string? Error = null, string? Screenshot = null);
public record BrowserIdentity(string Account, bool LoggedIn);
public sealed class AgentException(string message) : Exception(message);

public static partial class Rules
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")] private static partial Regex VideoIdPattern();
    [GeneratedRegex(@"https?://|www\.|\b[\w-]+\.(com|net|org|kr|io)\b|#", RegexOptions.IgnoreCase)] private static partial Regex Links();
    [GeneratedRegex("[가-힣]")] private static partial Regex Korean();
    public static string ExtractVideoId(string input)
    {
        if (VideoIdPattern().IsMatch(input)) return input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "https" && uri.Scheme != "http") throw new AgentException("올바른 YouTube URL을 입력하세요.");
        var host = uri.Host.ToLowerInvariant();
        string? id = null;
        if (host == "youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == "shorts") id = parts[1];
            else if (uri.AbsolutePath == "/watch") id = uri.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).Where(x => x.Length == 2 && x[0] == "v").Select(x => Uri.UnescapeDataString(x[1])).FirstOrDefault();
        }
        if (id is null || !VideoIdPattern().IsMatch(id)) throw new AgentException("지원하지 않는 YouTube URL입니다.");
        return id;
    }
    public static int Count(string text) => new StringInfo(text).LengthInTextElements;
    public static string? ValidateComment(string text)
    {
        if (Count(text) is < 40 or > 120) return "댓글은 40~120자여야 합니다.";
        if (!Korean().IsMatch(text)) return "한국어 댓글을 작성하세요.";
        if (Links().IsMatch(text)) return "링크와 해시태그는 사용할 수 없습니다.";
        if (!text.Contains("운명원") || !text.Contains("홍보")) return "브랜드와 홍보 목적을 명시하세요 (예: 운명원 홍보).";
        return null;
    }
    public static double? Growth(ViewSnapshot first, ViewSnapshot last) => last.CapturedAt > first.CapturedAt && last.Views >= first.Views ? (last.Views - first.Views) / (last.CapturedAt - first.CapturedAt).TotalHours : null;
    public static string SeoulDate(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul")).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static bool CanTransition(PostStatus from, PostStatus to) => (from, to) switch
    {
        (PostStatus.Pending, PostStatus.AwaitingApproval) or
        (PostStatus.AwaitingApproval, PostStatus.OpeningBrowser) or
        (PostStatus.OpeningBrowser, PostStatus.Typing) or
        (PostStatus.Typing, PostStatus.Posting) or
        (PostStatus.Posting, PostStatus.Verifying) or
        (PostStatus.Verifying, PostStatus.Success) => true,
        (_, PostStatus.VerificationUnknown) => from is not (PostStatus.Success or PostStatus.Failed),
        (_, PostStatus.Failed) => from is PostStatus.Pending or PostStatus.AwaitingApproval or PostStatus.OpeningBrowser or PostStatus.Typing,
        _ => false
    };
}


