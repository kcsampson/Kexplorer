using Kexplorer.Core.Launching;
using Kexplorer.Core.Plugins;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class PluginManagerTests
{
    private sealed class TestFolderPlugin : IFolderPlugin
    {
        public string Name => "Test Folder Plugin";
        public string Description => "A test plugin";
        public bool IsActive => true;
        public PluginShortcut? Shortcut => null;

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestFilePlugin : IFilePlugin
    {
        public string Name => "Test File Plugin";
        public string Description => "A test plugin";
        public bool IsActive => true;
        public PluginShortcut? Shortcut => null;

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ExecuteAsync(string folderPath, IReadOnlyList<Core.FileSystem.FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public void Register_FolderPlugin_AddsToList()
    {
        var mgr = new PluginManager();
        var plugin = new TestFolderPlugin();
        mgr.Register(plugin);

        Assert.Single(mgr.FolderPlugins);
        Assert.Equal("Test Folder Plugin", mgr.FolderPlugins[0].Name);
    }

    [Fact]
    public void Register_FilePlugin_AddsToList()
    {
        var mgr = new PluginManager();
        var plugin = new TestFilePlugin();
        mgr.Register(plugin);

        Assert.Single(mgr.FilePlugins);
        Assert.Empty(mgr.FolderPlugins);
    }

    [Fact]
    public void ScanAssembly_FindsPlugins()
    {
        var mgr = new PluginManager();
        mgr.ScanAssembly(typeof(Kexplorer.Plugins.BuiltInPluginMarker).Assembly);

        // We registered multiple built-in plugins
        Assert.True(mgr.FolderPlugins.Count >= 5, $"Expected at least 5 folder plugins, got {mgr.FolderPlugins.Count}");
        Assert.True(mgr.FilePlugins.Count >= 5, $"Expected at least 5 file plugins, got {mgr.FilePlugins.Count}");
        Assert.True(mgr.ServicePlugins.Count >= 4, $"Expected at least 4 service plugins, got {mgr.ServicePlugins.Count}");
    }

    [Fact]
    public void ScanAssembly_FindsTerminalPluginsWithMenuGroup()
    {
        var mgr = new PluginManager();
        mgr.ScanAssembly(typeof(Kexplorer.Plugins.BuiltInPluginMarker).Assembly);

        var terminalPlugins = mgr.FolderPlugins
            .OfType<IMenuGroupPlugin>()
            .Where(p => p.MenuGroup == "Open Terminal Here")
            .ToList();

        Assert.Equal(4, terminalPlugins.Count);
    }

    [Theory]
    [InlineData(@"C:\Users\dev\projects", "/mnt/c/Users/dev/projects")]
    [InlineData(@"D:\data", "/mnt/d/data")]
    [InlineData(@"E:\", "/mnt/e/")]
    [InlineData("", "")]
    public void ToWslPath_ConvertsWindowsPathsCorrectly(string windowsPath, string expected)
    {
        Assert.Equal(expected, Kexplorer.Plugins.BuiltIn.WindowsTerminalHelper.ToWslPath(windowsPath));
    }

    [Fact]
    public void ScanAssembly_OpenInProjectEditor_IsOnlyFolderPlugin()
    {
        var mgr = new PluginManager();
        mgr.ScanAssembly(typeof(Kexplorer.Plugins.BuiltInPluginMarker).Assembly);

        // OpenInProjectEditorPlugin should appear in folder plugins
        Assert.Contains(mgr.FolderPlugins, p => p.Name == "Open in Project Editor");

        // OpenInProjectEditorPlugin should NOT appear in file plugins
        Assert.DoesNotContain(mgr.FilePlugins, p => p.Name == "Open in Project Editor");
    }

    [Fact]
    public void ScanAssembly_OpenExternalEditor_IsFilePlugin()
    {
        var mgr = new PluginManager();
        mgr.ScanAssembly(typeof(Kexplorer.Plugins.BuiltInPluginMarker).Assembly);

        // "Open External Editor" should be a file plugin
        Assert.Contains(mgr.FilePlugins, p => p.Name == "Open External Editor");
    }

    [Fact]
    public async Task LauncherService_Launch_NullCommand_DoesNotThrowNRE()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"kex_launch_null_{Guid.NewGuid():N}.json");
        try
        {
            // Config with a mapping that has an empty command
            var json = """
            {
                "launchers": [
                    { "ext": "testfoo", "command": "" }
                ]
            }
            """;
            await File.WriteAllTextAsync(tempFile, json);

            var svc = new LauncherService();
            await svc.LoadAsync(tempFile);

            // Create a temp file with the .testfoo extension
            var testFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.testfoo");
            await File.WriteAllTextAsync(testFile, "test");

            try
            {
                // This should not throw NullReferenceException - it may throw
                // Win32Exception if no shell handler, but not NRE
                svc.Launch(testFile);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Expected — no shell handler for .testfoo
            }
            catch (NullReferenceException)
            {
                Assert.Fail("LauncherService.Launch threw NullReferenceException for empty command mapping");
            }
            finally
            {
                File.Delete(testFile);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
