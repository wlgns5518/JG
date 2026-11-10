namespace MarketingAIAgent.Domain;

public interface IYouTubeService
{
    Task<SearchPage> SearchVideosAsync(string keyword, string order, string? pageToken, CancellationToken ct);
    Task<Video> GetVideoDetailsAsync(string id, CancellationToken ct);
    Task<Video> GetVideoByUrlAsync(string url, CancellationToken ct);
    Task<CommentSources> GetTopCommentsAsync(string id, CancellationToken ct);
}
public interface IAIAnalysisService
{
    Task<AnalysisOutput> AnalyzeVideoAsync(Video video, CommentSources comments, CancellationToken ct);
    Task<CommentDraft> GenerateCommentAsync(Video video, CommentSources comments, CancellationToken ct);
}
public interface ISecretStore { string Read(string name); void Write(string name, string value); }
public interface IAgentStore
{
    Task InitializeAsync(CancellationToken ct);
    Task<IReadOnlyList<Video>> VideosAsync(CancellationToken ct);
    Task SaveVideosAsync(IEnumerable<Video> videos, CancellationToken ct);
    Task SaveAnalysisAsync(AnalysisOutput output, CancellationToken ct);
    Task SaveDraftAsync(CommentDraft draft, CancellationToken ct);
    Task<AnalysisOutput?> LatestAnalysisAsync(string videoId, CancellationToken ct);
    Task<IReadOnlyList<ChromeProfile>> ProfilesAsync(CancellationToken ct);
    Task SaveProfileAsync(ChromeProfile profile, CancellationToken ct);
    Task<IReadOnlyList<CommentPost>> PostsAsync(CancellationToken ct);
    Task<CommentPost> ReserveAsync(string videoId, string profileId, string text, string account, int limit, CancellationToken ct);
    Task TransitionAsync(string id, PostStatus status, BrowserResult? result, CancellationToken ct);
    Task<int> DailyCountAsync(CancellationToken ct);
    Task<bool> IsBlockedAsync(string videoId, CancellationToken ct);
    Task<AgentSettings> SettingsAsync(CancellationToken ct);
    Task SaveSettingsAsync(AgentSettings settings, CancellationToken ct);
    Task LogAsync(string message, CancellationToken ct);
    Task<IReadOnlyList<ActivityLog>> LogsAsync(CancellationToken ct);
    Task<IReadOnlyList<ViewSnapshot>> SnapshotsAsync(string videoId, CancellationToken ct);
}
public interface IBrowserService : IAsyncDisposable
{
    Task<BrowserIdentity> OpenProfileAsync(ChromeProfile profile, CancellationToken ct);
    Task<BrowserResult> PostAsync(ChromeProfile profile, Video video, string text, Func<BrowserIdentity, Task<bool>> approve, Func<PostStatus, Task> progress, CancellationToken ct);
}
