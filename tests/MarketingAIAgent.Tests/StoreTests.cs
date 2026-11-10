using MarketingAIAgent.Domain;
using MarketingAIAgent.Infrastructure;
using Xunit;

namespace MarketingAIAgent.Tests;
public sealed class StoreTests : IAsyncLifetime
{
    readonly string file = Path.Combine(Path.GetTempPath(), "marketing-tests-" + Guid.NewGuid() + ".db");
    SqliteAgentStore store = null!;
    public async Task InitializeAsync() { store = new("Data Source=" + file); await store.InitializeAsync(default); }
    public Task DisposeAsync() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(file + suffix); return Task.CompletedTask; }
    Task<CommentPost> Reserve(string video = "abcdefghijk", int limit = 10) => store.ReserveAsync(video, "youtube1", RulesTests.ValidComment, "@test", limit, default);
    [Fact] public async Task PersistsAndDeduplicatesVideos()
    {
        await store.SaveVideosAsync([new Video { Id = "abcdefghijk", Title = "first", Views = 1000 }], default);
        await store.SaveVideosAsync([new Video { Id = "abcdefghijk", Title = "updated", Views = 1300 }], default);
        var reopened = new SqliteAgentStore("Data Source=" + file);
        Assert.Equal("updated", Assert.Single(await reopened.VideosAsync(default)).Title);
        Assert.Equal(2, (await store.SnapshotsAsync("abcdefghijk", default)).Count);
    }
    [Fact] public async Task BlocksDuplicateAcrossProfilesAndRestart()
    {
        await Reserve();
        var reopened = new SqliteAgentStore("Data Source=" + file); await reopened.InitializeAsync(default);
        await Assert.ThrowsAsync<AgentException>(() => reopened.ReserveAsync("abcdefghijk", "youtube2", RulesTests.ValidComment, "@other", 10, default));
        Assert.True(await reopened.IsBlockedAsync("abcdefghijk", default));
    }
    [Fact] public async Task EnforcesDailyLimitIncludingFailures()
    {
        var post = await Reserve(limit: 1); await store.TransitionAsync(post.Id, PostStatus.Failed, new(PostStatus.Failed, Error: "cancel"), default);
        await Assert.ThrowsAsync<AgentException>(() => Reserve("12345678901", 1));
        Assert.Equal(1, await store.DailyCountAsync(default));
    }
    [Fact] public async Task PreClickFailureCanBeRetried()
    {
        var post = await Reserve(); await store.TransitionAsync(post.Id, PostStatus.OpeningBrowser, null, default);
        await store.TransitionAsync(post.Id, PostStatus.Failed, new(PostStatus.Failed), default);
        Assert.False(await store.IsBlockedAsync(post.VideoId, default)); await Reserve();
        Assert.Equal(2, (await store.PostsAsync(default)).Count);
    }
    [Fact] public async Task UncertainClickNeverReposts()
    {
        var post = await Reserve();
        foreach (var state in new[] { PostStatus.OpeningBrowser, PostStatus.Typing, PostStatus.Posting, PostStatus.Verifying, PostStatus.VerificationUnknown }) await store.TransitionAsync(post.Id, state, null, default);
        await Assert.ThrowsAsync<AgentException>(() => Reserve());
    }
    [Fact] public async Task ConcurrentReservationsHaveOneWinner()
    {
        async Task<bool> Attempt() { try { await Reserve(); return true; } catch (AgentException) { return false; } }
        var results = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        Assert.Single(results, x => x); Assert.Single(await store.PostsAsync(default));
    }
    [Fact] public async Task ConcurrentDifferentVideosRespectLimit()
    {
        async Task<bool> Attempt(string id) { try { await Reserve(id, 1); return true; } catch (AgentException) { return false; } }
        var results = await Task.WhenAll(Task.Run(() => Attempt("abcdefghijk")), Task.Run(() => Attempt("12345678901")));
        Assert.Single(results, x => x);
    }
    [Fact] public async Task SavesAnalysisAndSettings()
    {
        var analysis = new VideoAnalysis { VideoId = "abcdefghijk", MainConcern = "고민" };
        await store.SaveAnalysisAsync(new(analysis, new() { AnalysisId = analysis.Id, VideoId = analysis.VideoId, Text = RulesTests.ValidComment }), default);
        Assert.Equal(RulesTests.ValidComment, (await store.LatestAnalysisAsync(analysis.VideoId, default))!.Draft.Text);
        await store.SaveSettingsAsync(new() { DailyLimit = 3, GeminiModel = "custom-model" }, default);
        Assert.Equal(3, (await store.SettingsAsync(default)).DailyLimit);
    }
}

