using MarketingAIAgent.Domain;

namespace MarketingAIAgent.Application;

public sealed class AgentWorkflow(IYouTubeService youtube, IAIAnalysisService ai, IAgentStore store, IBrowserService browser)
{
    public async Task<AnalysisOutput> AnalyzeAsync(Video video, CancellationToken ct)
    {
        var fresh = await youtube.GetVideoDetailsAsync(video.Id, ct);
        video.Title = fresh.Title; video.Description = fresh.Description; video.Channel = fresh.Channel;
        video.Thumbnail = fresh.Thumbnail; video.Views = fresh.Views; video.PublishedAt = fresh.PublishedAt;
        await store.SaveVideosAsync([fresh], ct);
        var sources = await youtube.GetTopCommentsAsync(video.Id, ct);
        var output = await ai.AnalyzeVideoAsync(fresh, sources, ct);
        await store.SaveAnalysisAsync(output, ct);
        return output;
    }
    public async Task<BrowserResult> PublishAsync(Video video, ChromeProfile profile, CommentDraft draft, Func<BrowserIdentity, Task<bool>> approve, IProgress<PostStatus> progress, CancellationToken ct)
    {
        var error = Rules.ValidateComment(draft.Text);
        if (error is not null) throw new AgentException(error);
        if (draft.VideoId != video.Id) throw new AgentException("영상과 초안이 일치하지 않습니다.");
        var saved = await store.LatestAnalysisAsync(video.Id, ct);
        if (saved is null || saved.Analysis.Id != draft.AnalysisId) throw new AgentException("현재 영상의 저장된 분석이 필요합니다.");
        var evidence = System.Text.Json.JsonSerializer.Deserialize<string[]>(saved.Analysis.EvidenceJson) ?? [];
        if (!evidence.Any(x => x.Length >= 4 && draft.Text.Contains(x, StringComparison.Ordinal) && (video.Title.Contains(x, StringComparison.Ordinal) || video.Description.Contains(x, StringComparison.Ordinal)))) throw new AgentException("수정한 댓글에 확인된 근거 구절을 포함하세요.");
        await store.SaveDraftAsync(draft, ct);
        var settings = await store.SettingsAsync(ct);
        var post = await store.ReserveAsync(video.Id, profile.Id, draft.Text, profile.Account, settings.DailyLimit, ct);
        var current = PostStatus.AwaitingApproval;
        async Task Move(PostStatus status)
        {
            await store.TransitionAsync(post.Id, status, null, CancellationToken.None);
            current = status;
            progress.Report(status);
        }
        try
        {
            await Move(PostStatus.OpeningBrowser);
            var result = await browser.PostAsync(profile, video, draft.Text, approve, Move, ct);
            await store.TransitionAsync(post.Id, result.Status, result, CancellationToken.None);
            progress.Report(result.Status);
            return result;
        }
        catch (Exception ex)
        {
            var status = current is PostStatus.Posting or PostStatus.Verifying ? PostStatus.VerificationUnknown : PostStatus.Failed;
            var result = new BrowserResult(status, Error: ex is OperationCanceledException ? "사용자 취소" : "게시 작업 오류. 활동 로그를 확인하세요.");
            await store.TransitionAsync(post.Id, status, result, CancellationToken.None);
            progress.Report(status);
            throw;
        }
    }
}
