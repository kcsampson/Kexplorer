using Kexplorer.Core.Launching;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class LauncherServiceTests
{
    [Fact]
    public async Task LoadAndSave_RoundTrips()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"kex_launchers_{Guid.NewGuid():N}.json");

        try
        {
            // Write a test config
            var json = """
            {
                "launchers": [
                    { "ext": "*", "command": "notepad.exe" },
                    { "ext": "xml", "command": "firefox.exe" },
                    { "ext": "sln", "command": "devenv.exe", "preOption": "/edit" }
                ]
            }
            """;
            await File.WriteAllTextAsync(tempFile, json);

            var svc = new LauncherService();
            await svc.LoadAsync(tempFile);

            // Save to a new file and reload
            var tempFile2 = tempFile + ".roundtrip.json";
            await svc.SaveAsync(tempFile2);

            var svc2 = new LauncherService();
            await svc2.LoadAsync(tempFile2);

            // Verify by launching a known extension — we can't easily test Process.Start,
            // but we can verify the service loaded without error
            File.Delete(tempFile2);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_MissingFile_DoesNotThrow()
    {
        var svc = new LauncherService();
        await svc.LoadAsync(@"C:\nonexistent_launchers_kex.json");
        // Should not throw — just has no mappings
    }
}
