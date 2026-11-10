using System.Windows;
namespace MarketingAIAgent.App;
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel model) { InitializeComponent(); DataContext = model; }
    async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        await ((MainViewModel)DataContext).SaveSettingsAsync(YouTubeKey.Password, GeminiKey.Password);
        YouTubeKey.Clear(); GeminiKey.Clear();
    }
    void OpenCommentLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri is null || e.Uri.Scheme != "https" || e.Uri.Host != "www.youtube.com") return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { ((MainViewModel)DataContext).Status = "기본 브라우저를 열지 못했습니다. 댓글 링크를 복사해 확인하세요."; }
    }
}
