using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using IComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Kexplorer.UI;

/// <summary>
/// Provides Shell-level clipboard access for reading file contents via COM IDataObject.
/// Uses FileGroupDescriptorW / FileContents clipboard formats, which enables file transfer
/// across RDP sessions (the RDP clipboard redirector streams file bytes through the virtual channel).
/// </summary>
internal static class ShellClipboardHelper
{
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FD_ATTRIBUTES = 0x04;

    [DllImport("ole32.dll")]
    private static extern int OleGetClipboard([MarshalAs(UnmanagedType.Interface)] out IComDataObject ppDataObj);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct FILEDESCRIPTOR
    {
        public uint dwFlags;
        public Guid clsid;
        public int cxSize;
        public int cySize;
        public int xPoint;
        public int yPoint;
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
    }

    /// <summary>
    /// Paste clipboard file contents to the specified folder.
    /// Reads FileGroupDescriptorW and FileContents from the Shell clipboard via COM.
    /// Runs on a background STA thread to avoid blocking the UI during RDP file transfer.
    /// </summary>
    public static Task<int> PasteToFolderAsync(
        string destinationFolder,
        Action<string>? statusCallback,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<int>();
        var thread = new Thread(() =>
        {
            OleInitialize(IntPtr.Zero);
            try
            {
                var count = PasteToFolderCore(destinationFolder, statusCallback, cancellationToken);
                tcs.SetResult(count);
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                OleUninitialize();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    private static int PasteToFolderCore(
        string destinationFolder,
        Action<string>? statusCallback,
        CancellationToken cancellationToken)
    {
        int hr = OleGetClipboard(out var dataObj);
        if (hr != 0)
            throw new InvalidOperationException($"Failed to access clipboard (HRESULT: 0x{hr:X8}).");

        try
        {
            var descriptors = ReadFileDescriptors(dataObj);
            if (descriptors.Count == 0)
                throw new InvalidOperationException(
                    "No file content found on clipboard. Copy files in the remote session first.");

            int pastedCount = 0;

            for (int i = 0; i < descriptors.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var desc = descriptors[i];
                var destPath = Path.Combine(destinationFolder, desc.cFileName);

                bool isDirectory = (desc.dwFlags & FD_ATTRIBUTES) != 0
                    && (desc.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;

                if (isDirectory)
                {
                    Directory.CreateDirectory(destPath);
                }
                else
                {
                    // Ensure parent directory exists (for nested files in folders)
                    var parentDir = Path.GetDirectoryName(destPath);
                    if (parentDir != null)
                        Directory.CreateDirectory(parentDir);

                    if (File.Exists(destPath))
                    {
                        statusCallback?.Invoke($"Skipped (already exists): {desc.cFileName}");
                        continue;
                    }

                    statusCallback?.Invoke($"Pasting ({i + 1}/{descriptors.Count}): {desc.cFileName}");
                    WriteFileContents(dataObj, i, destPath);
                    pastedCount++;
                }
            }

            return pastedCount;
        }
        finally
        {
            Marshal.ReleaseComObject(dataObj);
        }
    }

    private static List<FILEDESCRIPTOR> ReadFileDescriptors(IComDataObject dataObj)
    {
        var cfFormat = (short)RegisterClipboardFormat("FileGroupDescriptorW");
        var fmt = new FORMATETC
        {
            cfFormat = cfFormat,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            ptd = IntPtr.Zero,
            tymed = TYMED.TYMED_HGLOBAL
        };

        if (dataObj.QueryGetData(ref fmt) != 0)
            return new List<FILEDESCRIPTOR>();

        dataObj.GetData(ref fmt, out var medium);
        try
        {
            var ptr = GlobalLock(medium.unionmember);
            if (ptr == IntPtr.Zero)
                return new List<FILEDESCRIPTOR>();

            try
            {
                int count = Marshal.ReadInt32(ptr);
                var descriptors = new List<FILEDESCRIPTOR>(count);
                int descriptorSize = Marshal.SizeOf<FILEDESCRIPTOR>();
                var current = ptr + 4; // skip the count field

                for (int i = 0; i < count; i++)
                {
                    var desc = Marshal.PtrToStructure<FILEDESCRIPTOR>(current);
                    descriptors.Add(desc);
                    current += descriptorSize;
                }

                return descriptors;
            }
            finally
            {
                GlobalUnlock(medium.unionmember);
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static void WriteFileContents(IComDataObject dataObj, int index, string destPath)
    {
        var cfFormat = (short)RegisterClipboardFormat("FileContents");
        var fmt = new FORMATETC
        {
            cfFormat = cfFormat,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = index,
            ptd = IntPtr.Zero,
            tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL
        };

        dataObj.GetData(ref fmt, out var medium);

        try
        {
            if (medium.tymed == TYMED.TYMED_ISTREAM && medium.unionmember != IntPtr.Zero)
            {
                WriteFromIStream(medium.unionmember, destPath);
            }
            else if (medium.tymed == TYMED.TYMED_HGLOBAL && medium.unionmember != IntPtr.Zero)
            {
                WriteFromHGlobal(medium.unionmember, destPath);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported clipboard medium type ({medium.tymed}) for file at index {index}.");
            }
        }
        finally
        {
            ReleaseStgMedium(ref medium);
        }
    }

    private static void WriteFromIStream(IntPtr pStream, string destPath)
    {
        var comStream = (IStream)Marshal.GetObjectForIUnknown(pStream);
        try
        {
            using var fileStream = File.Create(destPath);
            var buffer = new byte[81920];
            var pcbRead = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                while (true)
                {
                    Marshal.WriteInt32(pcbRead, 0);
                    comStream.Read(buffer, buffer.Length, pcbRead);
                    int bytesRead = Marshal.ReadInt32(pcbRead);
                    if (bytesRead <= 0) break;
                    fileStream.Write(buffer, 0, bytesRead);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pcbRead);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(comStream);
        }
    }

    private static void WriteFromHGlobal(IntPtr hGlobal, string destPath)
    {
        var ptr = GlobalLock(hGlobal);
        if (ptr == IntPtr.Zero) return;

        try
        {
            var size = (int)GlobalSize(hGlobal).ToUInt64();
            using var fileStream = File.Create(destPath);
            var buffer = new byte[Math.Min(size, 81920)];
            int offset = 0;
            while (offset < size)
            {
                int chunk = Math.Min(buffer.Length, size - offset);
                Marshal.Copy(ptr + offset, buffer, 0, chunk);
                fileStream.Write(buffer, 0, chunk);
                offset += chunk;
            }
        }
        finally
        {
            GlobalUnlock(hGlobal);
        }
    }
}
