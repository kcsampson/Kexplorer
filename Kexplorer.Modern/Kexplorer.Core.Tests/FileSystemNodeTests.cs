using Kexplorer.Core.FileSystem;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class FileSystemNodeTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var node = new FileSystemNode("test", @"C:\test", isDirectory: true);
        Assert.Equal("test", node.Name);
        Assert.Equal(@"C:\test", node.FullPath);
        Assert.True(node.IsDirectory);
        Assert.False(node.Stale);
        Assert.False(node.Loaded);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void DriveLetter_ExtractsCorrectly()
    {
        var node = new FileSystemNode("C:\\", @"C:\", isDirectory: true);
        Assert.Equal("C", node.DriveLetter);

        var node2 = new FileSystemNode("test", "/unix/path", isDirectory: true);
        Assert.Null(node2.DriveLetter);
    }

    [Fact]
    public void MarkStale_CascadesToChildren()
    {
        var parent = new FileSystemNode("parent", @"C:\parent", isDirectory: true);
        var child = new FileSystemNode("child", @"C:\parent\child", isDirectory: true);
        var grandchild = new FileSystemNode("gc", @"C:\parent\child\gc", isDirectory: true);

        parent.Children.Add(child);
        child.Children.Add(grandchild);

        parent.MarkStale();

        Assert.True(parent.Stale);
        Assert.True(child.Stale);
        Assert.True(grandchild.Stale);
    }

    [Fact]
    public void Stale_SetFalse_DoesNotCascade()
    {
        var parent = new FileSystemNode("parent", @"C:\parent", isDirectory: true);
        var child = new FileSystemNode("child", @"C:\parent\child", isDirectory: true);
        parent.Children.Add(child);

        parent.MarkStale();
        parent.Stale = false;

        Assert.False(parent.Stale);
        Assert.True(child.Stale); // child stays stale
    }
}
