using System.Text.Json;
using MarketingAIAgent.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAIAgent.Infrastructure;

public sealed class AgentDb(DbContextOptions<AgentDb> options) : DbContext(options)
{
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<ViewSnapshot> ViewSnapshots => Set<ViewSnapshot>();
    public DbSet<VideoAnalysis> VideoAnalyses => Set<VideoAnalysis>();
    public DbSet<CommentDraft> CommentDrafts => Set<CommentDraft>();
    public DbSet<ChromeProfile> ChromeProfiles => Set<ChromeProfile>();
    public DbSet<CommentPost> CommentPosts => Set<CommentPost>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Video>().Ignore(x => x.Url).Ignore(x => x.Summary);
        b.Entity<CommentPost>().Property(x => x.Status).HasConversion<string>();
        b.Entity<CommentPost>().HasIndex(x => x.GuardVideoId).IsUnique();
        b.Entity<CommentPost>().HasIndex(x => x.SeoulDate);
        b.Entity<ViewSnapshot>().HasIndex(x => new { x.VideoId, x.CapturedAt });
        b.Entity<VideoAnalysis>().HasIndex(x => new { x.VideoId, x.CreatedAt });
    }
}
public sealed class SqliteAgentStore(string connectionString, AppPaths? paths = null) : IAgentStore
{
    AgentDb Open() => new(new DbContextOptionsBuilder<AgentDb>().UseSqlite(connectionString).Options);
    public async Task InitializeAsync(CancellationToken ct)
    {
        await using var db = Open();
        await db.Database.EnsureCreatedAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
        // Never clear live reservations when a second instance starts. Convert only during explicit recovery.
        if (!await db.ChromeProfiles.AnyAsync(ct))
        {
            for (var i = 1; i <= 3; i++) db.ChromeProfiles.Add(new ChromeProfile { Id = "youtube" + i, Name = "유튜브" + i, DataPath = Path.Combine(paths?.Profiles ?? Path.Combine(Path.GetTempPath(), "MarketingAIAgentProfiles"), "youtube" + i) });
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task<IReadOnlyList<Video>> VideosAsync(CancellationToken ct) { await using var db = Open(); return await db.Videos.AsNoTracking().OrderByDescending(x => x.PublishedAt).Take(300).ToListAsync(ct); }
    public async Task SaveVideosAsync(IEnumerable<Video> videos, CancellationToken ct)
    {
        await using var db = Open();
        foreach (var video in videos.DistinctBy(x => x.Id))
        {
            var old = await db.Videos.FindAsync([video.Id], ct);
            if (old is null) db.Videos.Add(video); else db.Entry(old).CurrentValues.SetValues(video);
            db.ViewSnapshots.Add(new ViewSnapshot { VideoId = video.Id, Views = video.Views, CapturedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync(ct);
    }
    public async Task SaveAnalysisAsync(AnalysisOutput output, CancellationToken ct)
    {
        await using var db = Open(); db.VideoAnalyses.Add(output.Analysis); db.CommentDrafts.Add(output.Draft); await db.SaveChangesAsync(ct);
    }
    public async Task SaveDraftAsync(CommentDraft draft, CancellationToken ct)
    {
        await using var db = Open(); var old = await db.CommentDrafts.FindAsync([draft.Id], ct);
        if (old is null) db.CommentDrafts.Add(draft); else db.Entry(old).CurrentValues.SetValues(draft);
        await db.SaveChangesAsync(ct);
    }
    public async Task<AnalysisOutput?> LatestAnalysisAsync(string videoId, CancellationToken ct)
    {
        await using var db = Open();
        var analysis = await db.VideoAnalyses.AsNoTracking().Where(x => x.VideoId == videoId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (analysis is null) return null;
        var draft = await db.CommentDrafts.AsNoTracking().Where(x => x.AnalysisId == analysis.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return draft is null ? null : new(analysis, draft);
    }
    public async Task<IReadOnlyList<ChromeProfile>> ProfilesAsync(CancellationToken ct) { await using var db = Open(); return await db.ChromeProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct); }
    public async Task SaveProfileAsync(ChromeProfile profile, CancellationToken ct)
    {
        await using var db = Open(); var old = await db.ChromeProfiles.FindAsync([profile.Id], ct);
        if (old is null)
        {
            if (await db.ChromeProfiles.CountAsync(ct) >= 3) throw new AgentException("최대 3개 프로필만 등록할 수 있습니다.");
            db.ChromeProfiles.Add(profile);
        }
        else db.Entry(old).CurrentValues.SetValues(profile);
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<CommentPost>> PostsAsync(CancellationToken ct) { await using var db = Open(); return await db.CommentPosts.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync(ct); }
    public async Task<CommentPost> ReserveAsync(string videoId, string profileId, string text, string account, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 10) throw new AgentException("하루 게시 상한은 1~10개입니다.");
        var validation = Rules.ValidateComment(text);
        if (validation is not null) throw new AgentException(validation);
        await using var db = Open();
        await db.Database.OpenConnectionAsync(ct);
        // BEGIN IMMEDIATE serializes the check-and-reserve operation across processes.
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(tx, ct);
        if (await db.CommentPosts.AnyAsync(x => x.GuardVideoId == videoId, ct)) throw new AgentException("이미 게시했거나 결과 미확인/진행 중인 영상입니다. 자동 재게시가 차단됩니다.");
        var day = Rules.SeoulDate(DateTime.UtcNow);
        if (await db.CommentPosts.CountAsync(x => x.SeoulDate == day, ct) >= limit) throw new AgentException("오늘 게시 시도 상한에 도달했습니다 (Asia/Seoul). 실패/취소 시도도 포함됩니다.");
        if (!await db.ChromeProfiles.AnyAsync(x => x.Id == profileId, ct)) throw new AgentException("등록된 프로필을 선택하세요.");
        if (await db.CommentPosts.AnyAsync(x => x.CommentText == text && x.Status == PostStatus.Success, ct)) throw new AgentException("이미 게시한 동일 댓글입니다. 영상 맥락에 맞게 수정하세요.");
        var post = new CommentPost { VideoId = videoId, ProfileId = profileId, CommentText = text, Account = account, SeoulDate = day, Status = PostStatus.AwaitingApproval, GuardVideoId = videoId };
        db.CommentPosts.Add(post);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return post;
    }
    public async Task TransitionAsync(string id, PostStatus status, BrowserResult? result, CancellationToken ct)
    {
        await using var db = Open();
        var post = await db.CommentPosts.SingleAsync(x => x.Id == id, ct);
        if (!Rules.CanTransition(post.Status, status)) throw new AgentException($"유효하지 않은 게시 상태 전이: {post.Status} → {status}");
        post.Status = status;
        if (status == PostStatus.Posting) post.PostedAt = DateTime.UtcNow;
        if (status == PostStatus.Success) post.VerifiedAt = DateTime.UtcNow;
        if (status == PostStatus.Failed) post.GuardVideoId = null;
        if (result is not null) { post.CommentUrl = result.Url; post.ErrorMessage = result.Error; post.ScreenshotPath = result.Screenshot; }
        db.ActivityLogs.Add(new ActivityLog { Message = $"게시 {post.VideoId}: {status}" });
        await db.SaveChangesAsync(ct);
    }
    public async Task<int> DailyCountAsync(CancellationToken ct) { await using var db = Open(); var day = Rules.SeoulDate(DateTime.UtcNow); return await db.CommentPosts.CountAsync(x => x.SeoulDate == day, ct); }
    public async Task<bool> IsBlockedAsync(string videoId, CancellationToken ct) { await using var db = Open(); return await db.CommentPosts.AnyAsync(x => x.GuardVideoId == videoId, ct); }
    public async Task<AgentSettings> SettingsAsync(CancellationToken ct)
    {
        await using var db = Open(); var row = await db.AppSettings.FindAsync(["settings"], ct);
        return row is null ? new AgentSettings { DatabasePath = paths?.Database ?? "", LogPath = paths?.Logs ?? "", ProfileRoot = paths?.Profiles ?? "" } : JsonSerializer.Deserialize<AgentSettings>(row.Json) ?? new();
    }
    public async Task SaveSettingsAsync(AgentSettings settings, CancellationToken ct)
    {
        if (settings.DailyLimit is < 1 or > 10) throw new AgentException("하루 상한은 1~10개입니다.");
        if (string.IsNullOrWhiteSpace(settings.GeminiModel)) throw new AgentException("Gemini 모델 이름을 입력하세요.");
        await using var db = Open(); var row = await db.AppSettings.FindAsync(["settings"], ct);
        if (row is null) db.AppSettings.Add(new AppSetting { Json = JsonSerializer.Serialize(settings) }); else row.Json = JsonSerializer.Serialize(settings);
        await db.SaveChangesAsync(ct);
        paths?.SaveBootstrap(settings);
    }
    public async Task LogAsync(string message, CancellationToken ct) { await using var db = Open(); db.ActivityLogs.Add(new ActivityLog { Message = message }); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<ActivityLog>> LogsAsync(CancellationToken ct) { await using var db = Open(); return await db.ActivityLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(300).ToListAsync(ct); }
    public async Task<IReadOnlyList<ViewSnapshot>> SnapshotsAsync(string videoId, CancellationToken ct) { await using var db = Open(); return await db.ViewSnapshots.AsNoTracking().Where(x => x.VideoId == videoId).OrderBy(x => x.CapturedAt).ToListAsync(ct); }
}
