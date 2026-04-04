using Kexplorer.Core.FileSystem;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class DirectoryLoaderTests
{
    [Fact]
    public void LoadChildren_ReturnsSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kex_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sub1 = Directory.CreateDirectory(Path.Combine(tempDir, "sub1"));
        var sub2 = Directory.CreateDirectory(Path.Combine(tempDir, "sub2"));

        try
        {
            var children = DirectoryLoader.LoadChildren(tempDir, recurseDepth: 1);

            Assert.Equal(2, children.Count);
            Assert.All(children, c => Assert.True(c.IsDirectory));
            Assert.Contains(children, c => c.Name == "sub1");
            Assert.Contains(children, c => c.Name == "sub2");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadChildren_RecursesMultipleLevels()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kex_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "a", "b"));

        try
        {
            var children = DirectoryLoader.LoadChildren(tempDir, recurseDepth: 2);

            Assert.Single(children);
            Assert.Equal("a", children[0].Name);
            Assert.Single(children[0].Children);
            Assert.Equal("b", children[0].Children[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadFiles_ReturnsFileEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kex_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "file1.txt"), "a");
        File.WriteAllText(Path.Combine(tempDir, "file2.cs"), "b");

        try
        {
            var files = DirectoryLoader.LoadFiles(tempDir);

            Assert.Equal(2, files.Count);
            Assert.Contains(files, f => f.Name == "file1.txt");
            Assert.Contains(files, f => f.Name == "file2.cs");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadChildren_MissingDirectory_ReturnsEmpty()
    {
        var result = DirectoryLoader.LoadChildren(@"C:\this_path_does_not_exist_kexplorer_test");
        Assert.Empty(result);
    }

    [Fact]
    public void LoadFiles_MissingDirectory_ReturnsEmpty()
    {
        var result = DirectoryLoader.LoadFiles(@"C:\this_path_does_not_exist_kexplorer_test");
        Assert.Empty(result);
    }

    [Fact]
    public void GetDriveNodes_ReturnsNodes()
    {
        var drives = DirectoryLoader.GetDriveNodes(new[] { "C:\\", "D:\\" });
        Assert.Equal(2, drives.Count);
        Assert.All(drives, d => Assert.True(d.IsDirectory));
    }
}
