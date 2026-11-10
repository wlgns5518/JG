using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketingAIAgent.Application;
using MarketingAIAgent.Domain;
using Serilog;

namespace MarketingAIAgent.App;

public partial class MainViewModel(IAgentStore store, IYouTubeService youtube, AgentWorkflow workflow, IBrowserService browser, ISecretStore secrets) : ObservableObject
{
    [ObservableProperty] string keyword = "연애 고민";
    [ObservableProperty] string videoUrl = "";
    [ObservableProperty] int sortIndex;
    [ObservableProperty] Video? selectedVideo;
    [ObservableProperty] ChromeProfile? selectedProfile;
    [ObservableProperty] string draftText = "";
    [ObservableProperty] string concern = "영상을 선택하고 AI 분석을 시작하세요.";
    [ObservableProperty] string evidence = "";
    [ObservableProperty] string sourceNotice = "자막 미확보 · 제목과 설명, 접근 가능한 댓글만 사용합니다.";
    [ObservableProperty] string sourceComments = "";
    [ObservableProperty] string analysisModel = "—";
    [ObservableProperty] string status = "준비됨";
    [ObservableProperty] string progress = "대기 → 승인 → Chrome → 입력 → 게시 → 검증";
    [ObservableProperty] string growth = "최소 두 시점의 실제 조회수로 계산합니다.";
    [ObservableProperty] bool isBusy;
    [ObservableProperty] bool approved;
    [ObservableProperty] string accountStatus = "프로필 로그인 상태를 확인하세요.";
    [ObservableProperty] string dailyStatus = "";
    [ObservableProperty] bool blocked;
    [ObservableProperty] int dailyCount;
    [ObservableProperty] string geminiModel = "gemini-3.1-flash-lite";
    [ObservableProperty] string chromePath = "";
    [ObservableProperty] string databasePath = "";
    [ObservableProperty] string logPath = "";
    [ObservableProperty] string profileRoot = "";
    [ObservableProperty] int dailyLimit = 10;
    [ObservableProperty] bool youTubeConfigured;
    [ObservableProperty] bool geminiConfigured;
    [ObservableProperty] bool explorationOnly;
    [ObservableProperty] string profileName = "";
    [ObservableProperty] string profileDataPath = "";
    public ObservableCollection<Video> Videos { get; } = [];
    public ObservableCollection<ChromeProfile> Profiles { get; } = [];
    public ObservableCollection<CommentPost> Posts { get; } = [];
    public ObservableCollection<ActivityLog> Logs { get; } = [];
    string? nextToken;
    string searchKeyword = "";
    string searchOrder = "date";
    CommentDraft? draft;
    VideoAnalysis? analysis;
    CancellationTokenSource? operation;
    long selectionVersion;
    public int CharacterCount => Rules.Count(DraftText);
    public string Validation => Rules.ValidateComment(DraftText) ?? "형식 검증 통과 · 실제 근거와 표현은 직접 확인하세요.";
    public bool CanPublish => !ExplorationOnly && !IsBusy && Approved && SelectedVideo is not null && SelectedProfile is not null && SelectedProfile.LoginStatus == "로그인 확인" && draft is not null && !Blocked && DailyCount < DailyLimit && Rules.ValidateComment(DraftText) is null && analysis is not null && JsonSerializer.Deserialize<string[]>(analysis.EvidenceJson)!.Any(x => DraftText.Contains(x, StringComparison.Ordinal));
    public bool CanAnalyze => !IsBusy && SelectedVideo is not null;
    public bool CanWork => !IsBusy;
    public bool CanMore => !IsBusy && nextToken is not null;
    public bool CanPrepare => !IsBusy && draft is not null && Rules.ValidateComment(DraftText) is null;
    void Notify() { OnPropertyChanged(nameof(CanPublish)); OnPropertyChanged(nameof(CanAnalyze)); OnPropertyChanged(nameof(CanWork)); OnPropertyChanged(nameof(CanMore)); OnPropertyChanged(nameof(CanPrepare)); PublishCommand.NotifyCanExecuteChanged(); AnalyzeCommand.NotifyCanExecuteChanged(); MoreCommand.NotifyCanExecuteChanged(); SearchCommand.NotifyCanExecuteChanged(); LoadUrlCommand.NotifyCanExecuteChanged(); LoginCommand.NotifyCanExecuteChanged(); PrepareCommand.NotifyCanExecuteChanged(); FindGrowthCommand.NotifyCanExecuteChanged(); }
    partial void OnIsBusyChanged(bool value) => Notify();
    partial void OnApprovedChanged(bool value) => Notify();
    partial void OnExplorationOnlyChanged(bool value) { Approved = false; Notify(); }
    partial void OnBlockedChanged(bool value) => Notify();
    partial void OnDailyCountChanged(int value) => Notify();
    partial void OnDailyLimitChanged(int value) { Approved = false; Notify(); }
    partial void OnDraftTextChanged(string value) { Approved = false; OnPropertyChanged(nameof(CharacterCount)); OnPropertyChanged(nameof(Validation)); Notify(); }
    partial void OnSelectedProfileChanged(ChromeProfile? value) { Approved = false; ProfileName = value?.Name ?? ""; ProfileDataPath = value?.DataPath ?? ""; AccountStatus = value is null ? "프로필을 선택하세요." : $"{value.Name} · {value.LoginStatus} · {value.Account}"; Notify(); }
    partial void OnSelectedVideoChanged(Video? value)
    {
        Approved = false; draft = null; analysis = null; DraftText = ""; Concern = "AI 분석을 시작하세요."; Evidence = ""; SourceComments = ""; AnalysisModel = "—"; Blocked = false; Notify();
        var version = ++selectionVersion;
        if (value is not null) _ = SelectAsync(value, version);
    }
    async Task SelectAsync(Video video, long version)
    {
        try
        {
            var saved = await store.LatestAnalysisAsync(video.Id, CancellationToken.None);
            var isBlocked = await store.IsBlockedAsync(video.Id, CancellationToken.None);
            var snapshots = await store.SnapshotsAsync(video.Id, CancellationToken.None);
            if (SelectedVideo?.Id != video.Id || version != selectionVersion) return;
            if (saved is not null) Apply(saved);
            Blocked = isBlocked;
            var first = snapshots.FirstOrDefault(); var last = snapshots.LastOrDefault();
            var rate = first is not null && last is not null && (last.CapturedAt - first.CapturedAt).TotalMinutes >= 1 ? Rules.Growth(first, last) : null;
            Growth = rate is null ? "증가율 측정 대기: 1분 이상 간격으로 조회수를 다시 수집하세요." : $"실측 증가율 {rate:N1}회/시간 · {(video.Views is >= 1000 and <= 5000 && rate >= 150 ? "성장 후보 (탐색·초안 전용)" : "성장 후보 조건 미충족")}";
            Notify();
        }
        catch { Status = "저장된 분석/조회수 이력을 불러오지 못했습니다."; }
    }
    void Apply(AnalysisOutput output)
    {
        selectionVersion++;
        analysis = output.Analysis; draft = output.Draft; DraftText = draft.Text; Concern = analysis.MainConcern;
        Evidence = string.Join("\n", JsonSerializer.Deserialize<string[]>(analysis.EvidenceJson) ?? []);
        SourceComments = string.Join("\n\n", JsonSerializer.Deserialize<string[]>(analysis.CommentsJson) ?? []);
        SourceNotice = analysis.SourceNotice; AnalysisModel = $"{analysis.Model} · 신뢰도 {analysis.Confidence:P0}"; Notify();
        OnPropertyChanged(nameof(SelectedVideo));
    }
    public async Task InitializeAsync()
    {
        var settings = await store.SettingsAsync(CancellationToken.None);
        GeminiModel = settings.GeminiModel; ChromePath = settings.ChromePath; DailyLimit = settings.DailyLimit; DatabasePath = settings.DatabasePath; LogPath = settings.LogPath; ProfileRoot = settings.ProfileRoot;
        YouTubeConfigured = !string.IsNullOrEmpty(secrets.Read("youtube")); GeminiConfigured = !string.IsNullOrEmpty(secrets.Read("gemini"));
        foreach (var video in await store.VideosAsync(CancellationToken.None)) Videos.Add(video);
        foreach (var profile in await store.ProfilesAsync(CancellationToken.None)) { profile.LoginStatus = "미확인"; Profiles.Add(profile); }
        SelectedProfile = Profiles.FirstOrDefault(); await RefreshAsync();
    }
    async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; operation = new();
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { Status = "작업이 취소되었습니다."; }
        catch (OperationCanceledException) { Status = "API 응답 시간이 초과되었습니다. 잠시 후 직접 다시 시도하세요."; }
        catch (Exception ex)
        {
            Status = ex switch { AgentException => ex.Message, HttpRequestException => "네트워크 연결 실패. 인터넷 연결을 확인하세요.", IOException => "파일 접근 실패. 저장 경로와 권한을 확인하세요.", _ => "작업 실패: " + ex.GetType().Name + ". 활동 기록을 확인하세요." };
            Log.Warning("Operation failed: {Type}", ex.GetType().Name);
            try { await store.LogAsync(Status, CancellationToken.None); } catch { }
        }
        finally { operation.Dispose(); operation = null; IsBusy = false; try { await RefreshAsync(); } catch { Status = "데이터베이스 이력을 새로 고치지 못했습니다."; } }
    }
    [RelayCommand] void Cancel() => operation?.Cancel();
    [RelayCommand(CanExecute = nameof(CanWork))] Task SearchAsync() => RunAsync(async ct =>
    {
        ExplorationOnly = false; Status = "YouTube 검색 중…"; searchKeyword = Keyword; searchOrder = SortIndex == 0 ? "date" : "viewCount";
        var page = await youtube.SearchVideosAsync(searchKeyword, searchOrder, null, ct);
        await store.SaveVideosAsync(page.Videos, ct); Videos.Clear(); foreach (var video in page.Videos) Videos.Add(video); nextToken = page.NextPageToken;
        Status = page.Videos.Count == 0 ? "검색 결과가 없습니다." : $"{Videos.Count}개 영상 로드 완료";
    });
    [RelayCommand(CanExecute = nameof(CanMore))] Task MoreAsync() => RunAsync(async ct =>
    {
        var page = await youtube.SearchVideosAsync(searchKeyword, searchOrder, nextToken, ct); await store.SaveVideosAsync(page.Videos, ct);
        foreach (var video in page.Videos.Where(x => Videos.All(v => v.Id != x.Id))) Videos.Add(video); nextToken = page.NextPageToken; Status = $"{Videos.Count}개 영상 로드 완료";
    });
    [RelayCommand(CanExecute = nameof(CanWork))] Task LoadUrlAsync() => RunAsync(async ct =>
    {
        ExplorationOnly = false;
        Status = "URL 영상 분석 중…"; var video = await youtube.GetVideoByUrlAsync(VideoUrl, ct); await store.SaveVideosAsync([video], ct);
        var old = Videos.FirstOrDefault(x => x.Id == video.Id); if (old is not null) Videos.Remove(old); Videos.Insert(0, video); SelectedVideo = video;
        Apply(await workflow.AnalyzeAsync(video, ct)); Status = "URL 분석 및 초안 저장 완료";
    });
    [RelayCommand(CanExecute = nameof(CanWork))] Task FindGrowthAsync() => RunAsync(async ct =>
    {
        ExplorationOnly = true; Status = "성장 후보 실측 조회수 수집 중…";
        var page = await youtube.SearchVideosAsync(Keyword, "date", null, ct);
        await store.SaveVideosAsync(page.Videos, ct);
        Videos.Clear(); nextToken = null; var waiting = 0;
        foreach (var video in page.Videos.Where(x => x.Views is >= 1000 and <= 5000))
        {
            var snapshots = await store.SnapshotsAsync(video.Id, ct);
            if (snapshots.Count < 2 || (snapshots[^1].CapturedAt - snapshots[0].CapturedAt).TotalMinutes < 1) { waiting++; continue; }
            if (Rules.Growth(snapshots[0], snapshots[^1]) >= 150) Videos.Add(video);
        }
        Status = $"성장 후보 {Videos.Count}개 · 측정 대기 {waiting}개. 1분 이후 다시 탐색하세요. 이 모드는 탐색·초안 전용입니다.";
    });
    [RelayCommand(CanExecute = nameof(CanAnalyze))] Task AnalyzeAsync() => RunAsync(async ct =>
    {
        var video = SelectedVideo!; Status = "실제 메타데이터·인기 댓글 수집 및 Gemini 분석 중…";
        var output = await workflow.AnalyzeAsync(video, ct); if (SelectedVideo?.Id == video.Id) Apply(output); Status = "분석 및 초안 저장 완료";
    });
    [RelayCommand] void Copy() { if (!string.IsNullOrWhiteSpace(DraftText)) { try { Clipboard.SetText(DraftText); Status = "댓글 복사 완료"; } catch { Status = "클립보드에 접근하지 못했습니다."; } } }
    [RelayCommand(CanExecute = nameof(CanPrepare))] Task PrepareAsync() => RunAsync(async ct => { draft!.Text = DraftText; await store.SaveDraftAsync(draft, ct); Approved = false; Status = "초안을 저장했습니다. 근거·계정을 확인한 뒤 게시 검토를 승인하세요."; });
    [RelayCommand(CanExecute = nameof(CanWork))] Task LoginAsync() => RunAsync(async ct =>
    {
        if (SelectedProfile is null) throw new AgentException("프로필을 선택하세요.");
        Status = "Chrome에서 직접 로그인하세요. 완료 후 로그인 확인을 다시 누르세요.";
        var identity = await browser.OpenProfileAsync(SelectedProfile, ct); AccountStatus = $"{SelectedProfile.Name} · {SelectedProfile.LoginStatus} · {identity.Account}"; Notify();
    });
    [RelayCommand(CanExecute = nameof(CanPublish))] Task PublishAsync() => RunAsync(async ct =>
    {
        var video = SelectedVideo!; var profile = SelectedProfile!; draft!.Text = DraftText; var text = draft.Text; Approved = false;
        var result = await workflow.PublishAsync(video, profile, draft, identity => System.Windows.Application.Current.Dispatcher.InvokeAsync(() => MessageBox.Show($"영상: {video.Title}\n계정: {identity.Account} ({profile.Name})\n\n{text}\n\n이 댓글을 실제 YouTube에 게시하시겠습니까?", "최종 게시 승인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes).Task, new Progress<PostStatus>(s => Progress = s.ToString()), ct);
        Status = result.Status == PostStatus.Success ? "게시 성공: " + result.Url : $"{result.Status}: {result.Error}";
        Blocked = await store.IsBlockedAsync(video.Id, CancellationToken.None);
    });
    [RelayCommand] Task RefreshAsync() => RefreshCoreAsync();
    async Task RefreshCoreAsync()
    {
        Posts.Clear(); foreach (var post in await store.PostsAsync(CancellationToken.None)) Posts.Add(post);
        Logs.Clear(); foreach (var log in await store.LogsAsync(CancellationToken.None)) Logs.Add(log);
        DailyCount = await store.DailyCountAsync(CancellationToken.None); DailyStatus = $"오늘 {DailyCount} / {DailyLimit}회 · 서울 기준 · 실패/취소 포함";
    }
    public Task SaveSettingsAsync(string youtubeKey, string geminiKey) => RunAsync(async ct =>
    {
        if (!string.IsNullOrWhiteSpace(youtubeKey)) secrets.Write("youtube", youtubeKey);
        if (!string.IsNullOrWhiteSpace(geminiKey)) secrets.Write("gemini", geminiKey);
        if (!Path.IsPathFullyQualified(DatabasePath) || !Path.IsPathFullyQualified(LogPath) || !Path.IsPathFullyQualified(ProfileRoot)) throw new AgentException("저장 경로는 절대 경로로 입력하세요.");
        await store.SaveSettingsAsync(new AgentSettings { GeminiModel = GeminiModel.Trim(), ChromePath = ChromePath.Trim(), DailyLimit = DailyLimit, DatabasePath = DatabasePath, LogPath = LogPath, ProfileRoot = ProfileRoot }, ct);
        YouTubeConfigured = !string.IsNullOrEmpty(secrets.Read("youtube")); GeminiConfigured = !string.IsNullOrEmpty(secrets.Read("gemini")); Status = "설정 저장 완료. 경로 변경은 재시작 후 적용되며 기존 데이터는 자동 이동하지 않습니다.";
    });
    [RelayCommand(CanExecute = nameof(CanWork))] Task SaveProfileAsync() => RunAsync(async ct =>
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(ProfileName) || !Path.IsPathFullyQualified(ProfileDataPath)) throw new AgentException("프로필 이름과 절대 데이터 경로를 입력하세요.");
        SelectedProfile.Name = ProfileName.Trim(); SelectedProfile.DataPath = ProfileDataPath.Trim(); SelectedProfile.LoginStatus = "미확인"; SelectedProfile.Account = "";
        await store.SaveProfileAsync(SelectedProfile, ct); AccountStatus = "프로필 저장 완료. 로그인 상태를 다시 확인하세요."; Notify();
    });
}




