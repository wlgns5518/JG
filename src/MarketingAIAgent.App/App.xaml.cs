using System.IO;
using System.Windows;
using MarketingAIAgent.Application;
using MarketingAIAgent.Domain;
using MarketingAIAgent.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MarketingAIAgent.App;
public partial class App : System.Windows.Application
{
    IHost? host;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { args.Handled = true; MessageBox.Show("작업 중 오류가 발생했습니다. 저장된 이력을 확인한 후 다시 시도하세요.", "Marketing AI Agent"); };
        try
        {
            var paths = new AppPaths();
            Log.Logger = new LoggerConfiguration().WriteTo.File(Path.Combine(paths.Logs, "agent-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14).CreateLogger();
            host = Host.CreateDefaultBuilder().UseSerilog().ConfigureServices(services =>
            {
                services.AddSingleton(paths);
                services.AddSingleton<ISecretStore, DpapiSecretStore>();
                services.AddSingleton<IAgentStore>(new SqliteAgentStore(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = paths.Database, DefaultTimeout = 15 }.ToString(), paths));
                services.AddHttpClient<IYouTubeService, YouTubeService>(client => client.Timeout = TimeSpan.FromSeconds(45)).RemoveAllLoggers();
                services.AddHttpClient<IAIAnalysisService, GeminiAnalysisService>(client => client.Timeout = TimeSpan.FromSeconds(90)).RemoveAllLoggers();
                services.AddSingleton<IBrowserService, BrowserService>(); services.AddTransient<AgentWorkflow>();
                services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
            }).Build();
            await host.StartAsync();
            await host.Services.GetRequiredService<IAgentStore>().InitializeAsync(CancellationToken.None);
            var window = host.Services.GetRequiredService<MainWindow>();
            MainWindow = window; window.Show();
            await host.Services.GetRequiredService<MainViewModel>().InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Startup failure: {Type}", ex.GetType().Name);
            MessageBox.Show("초기화 실패: 데이터베이스/로그 경로의 접근 권한과 설정 파일을 확인하세요.\n" + ex.GetType().Name, "Marketing AI Agent"); Shutdown(1);
        }
    }
    protected override async void OnExit(ExitEventArgs e)
    {
        if (host is not null) { await host.StopAsync(); if (host is IAsyncDisposable asyncHost) await asyncHost.DisposeAsync(); else host.Dispose(); }
        await Log.CloseAndFlushAsync(); base.OnExit(e);
    }
}
