using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Kexplorer.UI.Terminal;

/// <summary>
/// Wraps the Windows ConPTY API to create a real pseudo-terminal
/// that supports interactive programs like vim, bash, etc.
/// </summary>
public sealed class ConPtyTerminal : IDisposable
{
    private IntPtr _pseudoConsoleHandle;
    private SafeFileHandle _pipeIn;   // we write → this → PTY stdin
    private SafeFileHandle _pipeOut;  // PTY stdout → this → we read
    private SafeFileHandle _ptyInputRead;
    private SafeFileHandle _ptyOutputWrite;
    private IntPtr _processInfo;
    private Stream? _writer;
    private Stream? _reader;
    private bool _disposed;

    public Stream Writer => _writer ?? throw new ObjectDisposedException(nameof(ConPtyTerminal));
    public Stream Reader => _reader ?? throw new ObjectDisposedException(nameof(ConPtyTerminal));

    /// <summary>
    /// Start a shell process attached to a ConPTY pseudo-console.
    /// </summary>
    /// <param name="command">e.g. "wsl.exe -d Ubuntu" or "powershell.exe"</param>
    /// <param name="cols">Terminal width in columns</param>
    /// <param name="rows">Terminal height in rows</param>
    /// <param name="workingDirectory">Initial working directory (null = inherit)</param>
    public void Start(string command, short cols, short rows, string? workingDirectory = null)
    {
        // Create two pipe pairs: one for PTY input, one for PTY output
        CreatePipePair(out _ptyInputRead, out _pipeIn);
        CreatePipePair(out _pipeOut, out _ptyOutputWrite);

        // Create the pseudo console
        var size = new COORD { X = cols, Y = rows };
        int hr = CreatePseudoConsole(size, _ptyInputRead.DangerousGetHandle(),
            _ptyOutputWrite.DangerousGetHandle(), 0, out _pseudoConsoleHandle);
        if (hr != 0)
            throw new Win32Exception(hr, $"CreatePseudoConsole failed: 0x{hr:X8}");

        // Start the process attached to the pseudo console
        StartProcess(command, workingDirectory);

        // Close the PTY-side handles — the pseudo console owns them now
        _ptyInputRead.Dispose();
        _ptyOutputWrite.Dispose();

        _writer = new FileStream(_pipeIn, FileAccess.Write);
        _reader = new FileStream(_pipeOut, FileAccess.Read);
    }

    /// <summary>
    /// Resize the pseudo console (e.g., when the panel is resized).
    /// </summary>
    public void Resize(short cols, short rows)
    {
        if (_pseudoConsoleHandle == IntPtr.Zero) return;
        var size = new COORD { X = cols, Y = rows };
        ResizePseudoConsole(_pseudoConsoleHandle, size);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _writer?.Dispose();
        _reader?.Dispose();

        if (_processInfo != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_processInfo);
            Marshal.FreeHGlobal(_processInfo);
            _processInfo = IntPtr.Zero;
        }

        if (_pseudoConsoleHandle != IntPtr.Zero)
        {
            ClosePseudoConsole(_pseudoConsoleHandle);
            _pseudoConsoleHandle = IntPtr.Zero;
        }

        _pipeIn?.Dispose();
        _pipeOut?.Dispose();
    }

    private static void CreatePipePair(out SafeFileHandle read, out SafeFileHandle write)
    {
        if (!CreatePipe(out read, out write, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe failed");
    }

    private void StartProcess(string command, string? workingDirectory)
    {
        // Initialize the startup info with the pseudo console
        var startupInfo = new STARTUPINFOEX();
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        // Get the required attribute list size
        IntPtr lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);

        _processInfo = Marshal.AllocHGlobal(lpSize.ToInt32());
        if (!InitializeProcThreadAttributeList(_processInfo, 1, 0, ref lpSize))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed");

        // Set the pseudo console attribute
        if (!UpdateProcThreadAttribute(_processInfo, 0,
            (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, _pseudoConsoleHandle,
            (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed");

        startupInfo.lpAttributeList = _processInfo;

        if (!CreateProcess(null, command, IntPtr.Zero, IntPtr.Zero, false,
            EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, workingDirectory,
            ref startupInfo, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateProcess failed for: {command}");
    }

    #region Native Interop

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX, dwY, dwXSize, dwYSize;
        public int dwXCountChars, dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, IntPtr hInput,
        IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList,
        int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList,
        uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize,
        IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string? lpApplicationName,
        string lpCommandLine, IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    #endregion
}
