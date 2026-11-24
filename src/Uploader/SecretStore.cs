using System.Diagnostics;
using UploaderCore;

namespace Uploader;

/// <summary>
/// Kimlik bilgisini masaüstünün gizli bilgi hizmetinde (GNOME Anahtarlık vb.) saklar; libsecret'in
/// secret-tool aracı kullanılır. Hizmet yoksa yalnızca kullanıcının okuyabildiği bir dosyaya düşer.
/// </summary>
internal sealed class SecretStore : ICredentialStore
{
    private static readonly string[] Attributes = ["application", "tr.tall.CommonsDiversifiedUploader", "kind", "commons-auth"];
    private static readonly string FallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "commons-diversified-uploader", "credentials.json");

    private static (int Code, string Output) Run(string[] args, string? input = null)
    {
        try
        {
            var info = new ProcessStartInfo("secret-tool") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in args) info.ArgumentList.Add(a);
            using var process = Process.Start(info)!;
            if (input != null) process.StandardInput.Write(input);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(15000)) { process.Kill(); return (-1, ""); }
            return (process.ExitCode, output);
        }
        catch (Exception) { return (-1, ""); }
    }

    public string? Load()
    {
        var (code, output) = Run(["lookup", .. Attributes]);
        if (code == 0 && output.Length > 0) return output;
        return File.Exists(FallbackPath) ? File.ReadAllText(FallbackPath) : null;
    }

    public void Save(string value)
    {
        if (Run(["store", "--label=Commons Diversified Uploader", .. Attributes], value).Code == 0)
        {
            File.Delete(FallbackPath);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FallbackPath)!);
        File.WriteAllText(FallbackPath, value);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(FallbackPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Clear()
    {
        Run(["clear", .. Attributes]);
        File.Delete(FallbackPath);
    }
}
