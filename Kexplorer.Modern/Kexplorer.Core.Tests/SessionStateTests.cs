using System.Text.Json;
using Kexplorer.Core.State;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class SessionStateTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"kex_state_{Guid.NewGuid():N}.json");

        try
        {
            var state = new SessionState
            {
                WindowWidth = 1200,
                WindowHeight = 800,
                WindowLeft = 100,
                WindowTop = 50,
                TreeSplitterPosition = 300,
                Tabs = new List<TabState>
                {
                    new()
                    {
                        TabName = "Main",
                        TabType = TabType.FileExplorer,
                        CurrentFolder = @"C:\Users",
                        Drives = new List<string> { "C:\\", "D:\\" },
                        IsSelected = true
                    },
                    new()
                    {
                        TabName = "Services",
                        TabType = TabType.Services,
                        VisibleServices = new List<string> { "MyService//."},
                        MachineName = ".",
                        IsSelected = false
                    }
                }
            };

            await SessionStateManager.SaveAsync(state, tempFile);
            var loaded = await SessionStateManager.LoadAsync(tempFile);

            Assert.Equal(1200, loaded.WindowWidth);
            Assert.Equal(800, loaded.WindowHeight);
            Assert.Equal(100, loaded.WindowLeft);
            Assert.Equal(50, loaded.WindowTop);
            Assert.Equal(300, loaded.TreeSplitterPosition);

            Assert.Equal(2, loaded.Tabs.Count);

            Assert.Equal("Main", loaded.Tabs[0].TabName);
            Assert.Equal(TabType.FileExplorer, loaded.Tabs[0].TabType);
            Assert.Equal(@"C:\Users", loaded.Tabs[0].CurrentFolder);
            Assert.Equal(2, loaded.Tabs[0].Drives.Count);
            Assert.True(loaded.Tabs[0].IsSelected);

            Assert.Equal("Services", loaded.Tabs[1].TabName);
            Assert.Equal(TabType.Services, loaded.Tabs[1].TabType);
            Assert.Single(loaded.Tabs[1].VisibleServices!);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsDefault()
    {
        var state = await SessionStateManager.LoadAsync(@"C:\nonexistent_kex_state.json");

        Assert.Single(state.Tabs);
        Assert.Equal("Main", state.Tabs[0].TabName);
        Assert.True(state.Tabs[0].IsSelected);
    }

    [Fact]
    public async Task Load_InvalidJson_ReturnsDefault()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"kex_state_{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, "{ invalid json }}");
            var state = await SessionStateManager.LoadAsync(tempFile);

            Assert.Single(state.Tabs);
            Assert.Equal("Main", state.Tabs[0].TabName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RootFolderPath_RoundTrips()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"kex_state_{Guid.NewGuid():N}.json");

        try
        {
            var state = new SessionState
            {
                Tabs = new List<TabState>
                {
                    new()
                    {
                        TabName = "AppData",
                        TabType = TabType.FileExplorer,
                        RootFolderPath = @"C:\Users\testuser\AppData",
                        IsSelected = true
                    },
                    new()
                    {
                        TabName = "Main",
                        TabType = TabType.FileExplorer,
                        Drives = new List<string> { "C:\\" },
                        IsSelected = false
                    }
                }
            };

            await SessionStateManager.SaveAsync(state, tempFile);
            var loaded = await SessionStateManager.LoadAsync(tempFile);

            Assert.Equal(2, loaded.Tabs.Count);

            // Rooted folder tab
            Assert.Equal("AppData", loaded.Tabs[0].TabName);
            Assert.Equal(@"C:\Users\testuser\AppData", loaded.Tabs[0].RootFolderPath);
            Assert.True(loaded.Tabs[0].IsSelected);

            // Standard tab has no RootFolderPath
            Assert.Equal("Main", loaded.Tabs[1].TabName);
            Assert.Null(loaded.Tabs[1].RootFolderPath);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
