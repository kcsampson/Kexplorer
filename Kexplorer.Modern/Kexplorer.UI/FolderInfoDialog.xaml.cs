using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32.SafeHandles;

namespace Kexplorer.UI;

public sealed class FolderActivityInfo
{
    public long SizeBytes { get; init; }
    public DateTime? LatestModifiedUtc { get; init; }
}

public partial class FolderInfoDialog : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly string _folderPath;
    private readonly Action<Dictionary<string, FolderActivityInfo>>? _onComplete;

    public FolderInfoDialog(string folderPath, Action<Dictionary<string, FolderActivityInfo>>? onComplete = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyToWindow(this);
        _folderPath = folderPath;
        _onComplete = onComplete;
        FolderPathText.Text = folderPath;
        Title = $"Info — {Path.GetFileName(folderPath)}";
        Loaded += (_, _) => _ = ScanAsync();
    }

    private async Task ScanAsync()
    {
        long fileCount = 0;
        long folderCount = 0;
        long totalSize = 0;
        long processedBytes = 0;
        var ct = _cts.Token;
        var lastUpdate = Environment.TickCount64;
        var seenFiles = new HashSet<FileIdentity>();

        // Track recursive folder stats for annotation back in the tree.
        var allDirectoryInfo = new Dictionary<string, FolderActivityInfo>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            var rootInfo = CalculateDirectoryInfo(
                _folderPath,
                allDirectoryInfo,
                ref fileCount,
                ref folderCount,
                ref processedBytes,
                seenFiles,
                ref lastUpdate,
                ct);
            totalSize = rootInfo.SizeBytes;
        }, ct);

        if (!ct.IsCancellationRequested)
        {
            UpdateDisplay(fileCount, folderCount, totalSize, scanning: false);
            _onComplete?.Invoke(allDirectoryInfo);
        }
    }

    private FolderActivityInfo CalculateDirectoryInfo(
        string path,
        Dictionary<string, FolderActivityInfo> allDirectoryInfo,
        ref long fileCount,
        ref long folderCount,
        ref long processedBytes,
        HashSet<FileIdentity> seenFiles,
        ref long lastUpdate,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return new FolderActivityInfo { SizeBytes = 0, LatestModifiedUtc = null };
        }

        long size = 0;
        DateTime? latestModifiedUtc = null;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (ct.IsCancellationRequested)
                {
                    return new FolderActivityInfo { SizeBytes = size, LatestModifiedUtc = latestModifiedUtc };
                }

                try
                {
                    var info = new FileInfo(file);

                    if (TryGetFileIdentity(file, out var fileIdentity) && !seenFiles.Add(fileIdentity))
                    {
                        continue;
                    }

                    size += info.Length;
                    fileCount++;
                    processedBytes += info.Length;

                    if (!latestModifiedUtc.HasValue || info.LastWriteTimeUtc > latestModifiedUtc.Value)
                    {
                        latestModifiedUtc = info.LastWriteTimeUtc;
                    }
                }
                catch { }

                var now = Environment.TickCount64;
                if (now - lastUpdate > 100)
                {
                    lastUpdate = now;
                    var fc = fileCount;
                    var dc = folderCount;
                    var progressSize = processedBytes;
                    Dispatcher.InvokeAsync(() => UpdateDisplay(fc, dc, progressSize, scanning: true));
                }
            }
        }
        catch { }

        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(path))
            {
                if (ct.IsCancellationRequested)
                {
                    return new FolderActivityInfo { SizeBytes = size, LatestModifiedUtc = latestModifiedUtc };
                }

                if (IsDirectoryLink(subDir))
                {
                    continue;
                }

                folderCount++;
                var subInfo = CalculateDirectoryInfo(subDir, allDirectoryInfo, ref fileCount, ref folderCount,
                    ref processedBytes, seenFiles, ref lastUpdate, ct);
                size += subInfo.SizeBytes;

                if (subInfo.LatestModifiedUtc.HasValue &&
                    (!latestModifiedUtc.HasValue || subInfo.LatestModifiedUtc.Value > latestModifiedUtc.Value))
                {
                    latestModifiedUtc = subInfo.LatestModifiedUtc.Value;
                }
            }
        }
        catch { }

        var folderInfo = new FolderActivityInfo
        {
            SizeBytes = size,
            LatestModifiedUtc = latestModifiedUtc
        };

        allDirectoryInfo[path] = folderInfo;

        return folderInfo;
    }

    private void UpdateDisplay(long fileCount, long folderCount, long totalSize, bool scanning)
    {
        FileCountText.Text = fileCount.ToString("N0");
        FolderCountText.Text = folderCount.ToString("N0");
        TotalSizeText.Text = FormatSize(totalSize);
        StatusText.Text = scanning ? "Scanning..." : "Complete";
    }

    private static string FormatSize(long bytes)
    {
        var mb = bytes / (1024.0 * 1024.0);
        return $"{mb:N1} MB";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts.Cancel();
    }

    private static bool TryGetFileIdentity(string filePath, out FileIdentity fileIdentity)
    {
        fileIdentity = default;

        try
        {
            using var handle = File.OpenHandle(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (!GetFileInformationByHandle(handle, out var info))
            {
                return false;
            }

            fileIdentity = new FileIdentity(
                info.VolumeSerialNumber,
                ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDirectoryLink(string directoryPath)
    {
        try
        {
            var attributes = File.GetAttributes(directoryPath);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle fileHandle,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    private readonly record struct FileIdentity(uint VolumeSerialNumber, ulong FileIndex);
}
