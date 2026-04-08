using Kexplorer.Core.FileSystem;
using Xunit;

namespace Kexplorer.Core.Tests;

public sealed class WslPathHelperTests
{
    [Fact]
    public void GetUncRoot_ReturnsCorrectPath()
    {
        Assert.Equal(@"\\wsl.localhost\Ubuntu", WslPathHelper.GetUncRoot("Ubuntu"));
        Assert.Equal(@"\\wsl.localhost\Debian", WslPathHelper.GetUncRoot("Debian"));
    }

    [Theory]
    [InlineData(@"\\wsl.localhost\Ubuntu", true)]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\user", true)]
    [InlineData(@"\\WSL.LOCALHOST\Ubuntu", true)]
    [InlineData(@"C:\Users\test", false)]
    [InlineData(@"\\server\share", false)]
    [InlineData("", false)]
    public void IsWslPath_DetectsCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, WslPathHelper.IsWslPath(path));
    }

    [Theory]
    [InlineData(@"\\wsl.localhost\Ubuntu", "Ubuntu")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\user", "Ubuntu")]
    [InlineData(@"\\wsl.localhost\Debian\etc\apt", "Debian")]
    [InlineData(@"C:\Users\test", null)]
    public void GetDistroName_ExtractsCorrectly(string path, string? expected)
    {
        Assert.Equal(expected, WslPathHelper.GetDistroName(path));
    }

    [Theory]
    [InlineData(@"\\wsl.localhost\Ubuntu", "/")]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\user\projects", "/home/user/projects")]
    [InlineData(@"\\wsl.localhost\Ubuntu\etc", "/etc")]
    [InlineData(@"\\wsl.localhost\Ubuntu\", "/")]
    [InlineData(@"C:\Users\test", @"C:\Users\test")]
    public void ToLinuxPath_ConvertsCorrectly(string uncPath, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ToLinuxPath(uncPath));
    }

    [Theory]
    [InlineData("/home/user/projects", "Ubuntu", @"\\wsl.localhost\Ubuntu\home\user\projects")]
    [InlineData("/etc", "Ubuntu", @"\\wsl.localhost\Ubuntu\etc")]
    [InlineData("/", "Ubuntu", @"\\wsl.localhost\Ubuntu")]
    [InlineData("", "Debian", @"\\wsl.localhost\Debian")]
    public void ToUncPath_ConvertsCorrectly(string linuxPath, string distro, string expected)
    {
        Assert.Equal(expected, WslPathHelper.ToUncPath(linuxPath, distro));
    }

    [Fact]
    public void DecomposePath_WslRoot_ReturnsSingleSegment()
    {
        var result = WslPathHelper.DecomposePath(@"\\wsl.localhost\Ubuntu");
        Assert.Single(result);
        Assert.Equal(@"\\wsl.localhost\Ubuntu", result[0]);
    }

    [Fact]
    public void DecomposePath_WslSubPath_ReturnsRootAndSegments()
    {
        var result = WslPathHelper.DecomposePath(@"\\wsl.localhost\Ubuntu\home\user\projects");
        Assert.Equal(4, result.Count);
        Assert.Equal(@"\\wsl.localhost\Ubuntu", result[0]);
        Assert.Equal("home", result[1]);
        Assert.Equal("user", result[2]);
        Assert.Equal("projects", result[3]);
    }

    [Fact]
    public void DecomposePath_NonWslPath_ReturnsEmpty()
    {
        var result = WslPathHelper.DecomposePath(@"C:\Users\test");
        Assert.Empty(result);
    }

    [Fact]
    public void RoundTrip_LinuxToUncToLinux()
    {
        var linuxPath = "/home/user/documents/code";
        var uncPath = WslPathHelper.ToUncPath(linuxPath, "Ubuntu");
        var backToLinux = WslPathHelper.ToLinuxPath(uncPath);
        Assert.Equal(linuxPath, backToLinux);
    }
}
