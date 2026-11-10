using System.Net;
using System.Text.Json;
using MarketingAIAgent.Domain;
using MarketingAIAgent.Infrastructure;
using Xunit;

namespace MarketingAIAgent.Tests;
public sealed class ApiTests
{
    sealed class Secrets(string key = "test-key") : ISecretStore { public string Read(string name) => key; public void Write(string name, string value) { } }
    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request));
    }
    [Fact] public async Task MissingKeyStopsBeforeNetwork()
    {
        var service = new YouTubeService(new HttpClient(new Handler(_ => throw new Exception("Must not call"))), new Secrets(""));
        await Assert.ThrowsAsync<AgentException>(() => service.GetVideoDetailsAsync("abcdefghijk", default));
    }
    [Fact] public async Task ParsesYouTubeMetadataAndUsesHeader()
    {
        var handler = new Handler(request =>
        {
            Assert.Equal("test-key", request.Headers.GetValues("X-Goog-Api-Key").Single());
            Assert.DoesNotContain("test-key", request.RequestUri!.ToString());
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"items":[{"id":"abcdefghijk","snippet":{"title":"연애 &amp; 고민","description":"설명","channelTitle":"채널","publishedAt":"2026-11-10T00:00:00Z","thumbnails":{"medium":{"url":"https://example.com/a.jpg"}}},"statistics":{"viewCount":"1500"}}]}""") };
        });
        var video = await new YouTubeService(new HttpClient(handler), new Secrets()).GetVideoDetailsAsync("abcdefghijk", default);
        Assert.Equal("연애 & 고민", video.Title); Assert.Equal(1500, video.Views);
    }
    [Theory][InlineData(403, "quotaExceeded", "할당량")][InlineData(429, "", "사용량")][InlineData(401, "", "인증")]
    public async Task DistinguishesApiFailures(int code, string reason, string message)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent(JsonSerializer.Serialize(new { error = new { errors = new[] { new { reason } } } })) };
        var ex = await Assert.ThrowsAsync<AgentException>(() => ApiJson.ReadAsync(response, "YouTube", default)); Assert.Contains(message, ex.Message);
    }
    [Fact] public async Task CommentsDisabledFallsBackExplicitly()
    {
        var service = new YouTubeService(new HttpClient(new Handler(_ => new(HttpStatusCode.Forbidden) { Content = new StringContent("""{"error":{"errors":[{"reason":"commentsDisabled"}]}}""") })), new Secrets());
        var sources = await service.GetTopCommentsAsync("abcdefghijk", default); Assert.Empty(sources.Comments); Assert.Contains("비활성화", sources.Notice);
    }
    static string Output(string evidence, string? comment = null) => JsonSerializer.Serialize(new { mainConcern = "관계 고민", evidence = new[] { evidence }, comment = comment ?? RulesTests.ValidComment, characterCount = 1, confidence = 0.8, sourceType = "metadata" });
    [Fact] public void RecalculatesCharacterCountAndChecksEvidence()
    {
        var output = GeminiAnalysisService.ParseOutput(Output("상대의 애매한 태도"), new() { Id = "abcdefghijk", Title = "상대의 애매한 태도와 연애 고민" }, new([], "댓글 없음"), "test-model");
        Assert.Equal(RulesTests.ValidComment, output.Draft.Text); Assert.Contains("자막 미확보", output.Analysis.SourceNotice);
    }
    [Fact] public void RejectsInventedEvidence() => Assert.Throws<AgentException>(() => GeminiAnalysisService.ParseOutput(Output("없는 장면을 지어냄"), new() { Title = "연애 고민" }, new([], ""), "test-model"));
    [Fact] public void RejectsEvidenceMissingFromComment() => Assert.Throws<AgentException>(() => GeminiAnalysisService.ParseOutput(Output("진짜 영상 제목"), new() { Title = "진짜 영상 제목" }, new([], ""), "test-model"));
    [Fact] public void RejectsMalformedAiOutput() => Assert.Throws<AgentException>(() => GeminiAnalysisService.ParseOutput("{}", new(), new([], ""), "test-model"));
}
