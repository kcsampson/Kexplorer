using Kexplorer.Core.FileSystem;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class FileEntryTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var entry = new FileEntry("test.txt", @"C:\folder\test.txt", 1024, new DateTime(2026, 1, 1), ".txt");

        Assert.Equal("test.txt", entry.Name);
        Assert.Equal(@"C:\folder\test.txt", entry.FullPath);
        Assert.Equal(1024, entry.Size);
        Assert.Equal(new DateTime(2026, 1, 1), entry.LastModified);
        Assert.Equal(".txt", entry.Extension);
    }

    [Fact]
    public void FromFileInfo_CreatesEntry()
    {
        // Use a known temp file
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "hello");
            var fi = new FileInfo(tempFile);
            var entry = FileEntry.FromFileInfo(fi);

            Assert.Equal(fi.Name, entry.Name);
            Assert.Equal(fi.FullName, entry.FullPath);
            Assert.Equal(fi.Length, entry.Size);
            Assert.Equal(fi.Extension.ToLowerInvariant(), entry.Extension);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
