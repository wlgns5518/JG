using MarketingAIAgent.Application;
using MarketingAIAgent.Domain;
using MarketingAIAgent.Infrastructure;
using Xunit;

namespace MarketingAIAgent.Tests;
public sealed class WorkflowTests : IAsyncLifetime
{
    readonly string file = Path.Combine(Path.GetTempPath(), "marketing-workflow-" + Guid.NewGuid() + ".db");
    SqliteAgentStore store = null!;
    readonly Video video = new() { Id = "abcdefghijk", Title = "상대의 애매한 태도와 관계 고민" };
    CommentDraft draft = null!;
    public async Task InitializeAsync()
    {
        store = new("Data Source=" + file); await store.InitializeAsync(default);
        var analysis = new VideoAnalysis { VideoId = video.Id, EvidenceJson = "[\"상대의 애매한 태도\"]" };
        draft = new() { VideoId = video.Id, AnalysisId = analysis.Id, Text = RulesTests.ValidComment };
        await store.SaveVideosAsync([video], default); await store.SaveAnalysisAsync(new(analysis, draft), default);
    }
    public Task DisposeAsync() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(file + suffix); return Task.CompletedTask; }
    sealed class Browser(bool afterClick) : IBrowserService
    {
        public Task<BrowserIdentity> OpenProfileAsync(ChromeProfile profile, CancellationToken ct) => Task.FromResult(new BrowserIdentity("@test", true));
        public async Task<BrowserResult> PostAsync(ChromeProfile profile, Video video, string text, Func<BrowserIdentity, Task<bool>> approve, Func<PostStatus, Task> progress, CancellationToken ct)
        {
            await progress(PostStatus.Typing);
            if (afterClick) await progress(PostStatus.Posting);
            throw new OperationCanceledException();
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    [Theory][InlineData(false, PostStatus.Failed)][InlineData(true, PostStatus.VerificationUnknown)]
    public async Task CancellationRecoveryPreservesClickBoundary(bool afterClick, PostStatus expected)
    {
        var workflow = new AgentWorkflow(null!, null!, store, new Browser(afterClick));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.PublishAsync(video, new() { Id = "youtube1" }, draft, _ => Task.FromResult(true), new Progress<PostStatus>(), default));
        Assert.Equal(expected, Assert.Single(await store.PostsAsync(default)).Status);
        Assert.Equal(afterClick, await store.IsBlockedAsync(video.Id, default));
    }
    [Fact] public async Task EditedCommentMustRetainEvidence()
    {
        draft.Text = RulesTests.ValidComment.Replace("상대의 애매한 태도", "관계에 관한 여러 생각");
        var workflow = new AgentWorkflow(null!, null!, store, new Browser(false));
        await Assert.ThrowsAsync<AgentException>(() => workflow.PublishAsync(video, new() { Id = "youtube1" }, draft, _ => Task.FromResult(true), new Progress<PostStatus>(), default));
        Assert.Empty(await store.PostsAsync(default));
    }
    sealed class Secrets : ISecretStore { public string Read(string name) => "fake-key"; public void Write(string name, string value) { } }
    sealed class GeminiHandler(int invalidResponses = 0) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("fake-key", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.DoesNotContain("fake-key", request.RequestUri!.ToString());
            using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("application/json", body.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
            Assert.Equal("object", body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("type").GetString());
            Calls++;
            var output = System.Text.Json.JsonSerializer.Serialize(new { mainConcern = "관계 고민", evidence = new[] { "상대의 애매한 태도" }, comment = Calls <= invalidResponses ? new string('가', 131) : RulesTests.ValidComment, characterCount = 80, confidence = 0.7, sourceType = "metadata" });
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = output } } } } } })) };
        }
    }
    [Fact] public async Task GeminiHttpUsesStructuredOutputAndParsesRealEnvelope()
    {
        var service = new GeminiAnalysisService(new HttpClient(new GeminiHandler()), new Secrets(), store);
        var output = await service.AnalyzeVideoAsync(video, new([], "댓글 없음"), default);
        Assert.Equal(video.Id, output.Draft.VideoId); Assert.Equal(RulesTests.ValidComment, output.Draft.Text);
    }
    [Fact] public async Task InvalidDraftIsRegeneratedAndRevalidated()
    {
        var handler = new GeminiHandler(1);
        var output = await new GeminiAnalysisService(new HttpClient(handler), new Secrets(), store).AnalyzeVideoAsync(video, new([], ""), default);
        Assert.Equal(2, handler.Calls); Assert.Equal(RulesTests.ValidComment, output.Draft.Text);
    }
    [Fact] public async Task RepeatedInvalidDraftStopsAfterThreeCalls()
    {
        var handler = new GeminiHandler(10);
        await Assert.ThrowsAsync<AgentException>(() => new GeminiAnalysisService(new HttpClient(handler), new Secrets(), store).AnalyzeVideoAsync(video, new([], ""), default));
        Assert.Equal(3, handler.Calls);
    }
}
