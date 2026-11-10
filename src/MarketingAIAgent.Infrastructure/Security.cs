using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketingAIAgent.Domain;

namespace MarketingAIAgent.Infrastructure;

public sealed class AppPaths
{
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarketingAIAgent");
    public string Database { get; }
    public string Logs { get; }
    public string Profiles { get; }
    public string Screenshots { get; }
    public string Bootstrap { get; }
    public AppPaths()
    {
        Directory.CreateDirectory(Root);
        Bootstrap = Path.Combine(Root, "paths.json");
        var saved = File.Exists(Bootstrap) ? JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(Bootstrap)) : null;
        Database = string.IsNullOrWhiteSpace(saved?.DatabasePath) ? Path.Combine(Root, "agent.db") : Path.GetFullPath(saved.DatabasePath);
        Logs = string.IsNullOrWhiteSpace(saved?.LogPath) ? Path.Combine(Root, "logs") : Path.GetFullPath(saved.LogPath);
        Profiles = string.IsNullOrWhiteSpace(saved?.ProfileRoot) ? Path.Combine(Root, "profiles") : Path.GetFullPath(saved.ProfileRoot);
        Screenshots = Path.Combine(Root, "screenshots");
        foreach (var path in new[] { Path.GetDirectoryName(Database)!, Logs, Profiles, Screenshots }) Directory.CreateDirectory(path);
    }
    public void SaveBootstrap(AgentSettings settings) => File.WriteAllText(Bootstrap, JsonSerializer.Serialize(settings));
}
public sealed class DpapiSecretStore(AppPaths paths) : ISecretStore
{
    string FilePath(string name) => Path.Combine(paths.Root, name switch { "youtube" => "youtube.protected", "gemini" => "gemini.protected", _ => throw new ArgumentException("Unknown secret") });
    public string Read(string name)
    {
        var file = FilePath(name);
        if (!File.Exists(file)) return "";
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser)); }
        catch (CryptographicException) { throw new AgentException("API 키를 복호화할 수 없습니다. 현재 Windows 계정에서 다시 저장하세요."); }
    }
    public void Write(string name, string value)
    {
        var file = FilePath(name);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser);
        var temp = file + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        File.Move(temp, file, true);
    }
}
