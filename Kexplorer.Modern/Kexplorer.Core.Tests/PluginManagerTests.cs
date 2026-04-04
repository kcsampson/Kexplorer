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
}
