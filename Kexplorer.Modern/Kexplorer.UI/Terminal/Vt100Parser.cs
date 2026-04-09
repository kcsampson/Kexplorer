using System.Text;
using System.Windows.Media;

namespace Kexplorer.UI.Terminal;

/// <summary>
/// Minimal VT100/ANSI terminal emulator.
/// Maintains a character buffer that the renderer can read.
/// Supports: cursor movement, erase, SGR colors, scrolling, alternate screen buffer.
/// </summary>
public sealed class Vt100Parser
{
    private TerminalCell[,] _buffer;        // [row, col]
    private TerminalCell[,]? _altBuffer;    // alternate screen buffer (vim, etc.)
    private bool _usingAltBuffer;
    private int _rows;
    private int _cols;
    private int _cursorRow;
    private int _cursorCol;
    private int _scrollTop;
    private int _scrollBottom;
    private bool _cursorVisible = true;
    private TerminalAttributes _currentAttr = new();

    // Scrollback history — lines that scrolled off the top of the main buffer
    private readonly List<TerminalCell[]> _scrollback = new();
    private const int MaxScrollbackLines = 10_000;

    // Cursor save/restore (DECSC / DECRC)
    private int _savedCursorRow;
    private int _savedCursorCol;
    private TerminalAttributes _savedAttr;

    // Parser state machine
    private ParseState _state = ParseState.Ground;
    private readonly StringBuilder _escParams = new();

    // UTF-8 multi-byte accumulator
    private int _utf8Remaining;       // bytes still expected in current codepoint
    private int _utf8Codepoint;       // accumulated codepoint value

    // Mode flags that the UI layer needs to query
    private bool _bracketedPasteMode; // ?2004
    private bool _applicationCursorKeys; // ?1  (DECCKM)

    public int Rows => _rows;
    public int Cols => _cols;
    public int CursorRow => _cursorRow;
    public int CursorCol => _cursorCol;
    public bool CursorVisible => _cursorVisible;

    /// <summary>Whether bracketed paste mode (?2004) is active — paste should be wrapped.</summary>
    public bool BracketedPasteMode => _bracketedPasteMode;

    /// <summary>Whether application cursor keys (?1 DECCKM) is active — arrows send ESC O x.</summary>
    public bool ApplicationCursorKeys => _applicationCursorKeys;

    /// <summary>Number of lines in the scrollback history.</summary>
    public int ScrollbackCount => _scrollback.Count;

    /// <summary>Total lines = scrollback + visible rows.</summary>
    public int TotalLineCount => _scrollback.Count + _rows;

    /// <summary>Fires when the buffer content changes and the UI should repaint.</summary>
    public event Action? BufferChanged;

    public Vt100Parser(int cols, int rows)
    {
        _cols = cols;
        _rows = rows;
        _buffer = new TerminalCell[rows, cols];
        _scrollTop = 0;
        _scrollBottom = rows - 1;
        InitBuffer(_buffer);
    }

    public void Resize(int cols, int rows)
    {
        var newBuf = new TerminalCell[rows, cols];
        InitBuffer(newBuf);
        // Copy existing content
        var copyRows = Math.Min(_rows, rows);
        var copyCols = Math.Min(_cols, cols);
        for (int r = 0; r < copyRows; r++)
            for (int c = 0; c < copyCols; c++)
                newBuf[r, c] = _buffer[r, c];

        _buffer = newBuf;
        _rows = rows;
        _cols = cols;
        _scrollTop = 0;
        _scrollBottom = rows - 1;
        _cursorRow = Math.Min(_cursorRow, rows - 1);
        _cursorCol = Math.Min(_cursorCol, cols - 1);

        if (_usingAltBuffer)
        {
            _altBuffer = new TerminalCell[rows, cols];
            InitBuffer(_altBuffer);
        }
    }

    public TerminalCell GetCell(int row, int col) => _buffer[row, col];

    /// <summary>
    /// Get a cell from the combined scrollback + active buffer.
    /// Line 0 = first scrollback line, ScrollbackCount = first visible row.
    /// </summary>
    public TerminalCell GetCellAbsolute(int absoluteRow, int col)
    {
        if (absoluteRow < _scrollback.Count)
        {
            var line = _scrollback[absoluteRow];
            return col < line.Length ? line[col] : new TerminalCell { Char = ' ' };
        }
        var bufRow = absoluteRow - _scrollback.Count;
        if (bufRow < _rows && col < _cols)
            return _buffer[bufRow, col];
        return new TerminalCell { Char = ' ' };
    }

    /// <summary>
    /// Feed raw bytes from the PTY output into the parser.
    /// </summary>
    public void Feed(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            ProcessByte(b);
        BufferChanged?.Invoke();
    }

    private void ProcessByte(byte b)
    {
        // --- UTF-8 multi-byte accumulator ---
        // If we're in the middle of a multi-byte sequence, collect continuation bytes.
        if (_utf8Remaining > 0)
        {
            if ((b & 0xC0) == 0x80) // valid continuation byte 10xxxxxx
            {
                _utf8Codepoint = (_utf8Codepoint << 6) | (b & 0x3F);
                _utf8Remaining--;
                if (_utf8Remaining == 0)
                {
                    // Complete codepoint — emit it
                    if (_utf8Codepoint <= 0xFFFF)
                        PutChar((char)_utf8Codepoint);
                    else if (_utf8Codepoint <= 0x10FFFF)
                    {
                        // Supplementary plane — store as replacement char (BMP-only cell buffer)
                        PutChar('\uFFFD');
                    }
                }
                return;
            }
            else
            {
                // Invalid continuation — discard incomplete sequence, re-process this byte
                _utf8Remaining = 0;
            }
        }

        // --- Detect UTF-8 lead bytes outside escape sequences ---
        if (_state == ParseState.Ground && b >= 0xC0)
        {
            if ((b & 0xE0) == 0xC0) { _utf8Codepoint = b & 0x1F; _utf8Remaining = 1; return; }
            if ((b & 0xF0) == 0xE0) { _utf8Codepoint = b & 0x0F; _utf8Remaining = 2; return; }
            if ((b & 0xF8) == 0xF0) { _utf8Codepoint = b & 0x07; _utf8Remaining = 3; return; }
            // Invalid lead byte — fall through to normal processing
        }

        switch (_state)
        {
            case ParseState.Ground:
                if (b == 0x1B) // ESC
                    _state = ParseState.Escape;
                else if (b == '\r')
                    _cursorCol = 0;
                else if (b == '\n')
                    LineFeed();
                else if (b == '\b')
                    { if (_cursorCol > 0) _cursorCol--; }
                else if (b == '\t')
                    _cursorCol = Math.Min((_cursorCol / 8 + 1) * 8, _cols - 1);
                else if (b == 0x07) // BEL — ignore
                    { }
                else if (b >= 0x20 && b < 0x80)
                    PutChar((char)b);
                break;

            case ParseState.Escape:
                if (b == '[')
                {
                    _escParams.Clear();
                    _state = ParseState.Csi;
                }
                else if (b == ']')
                {
                    _escParams.Clear();
                    _state = ParseState.Osc;
                }
                else if (b == '(')
                    _state = ParseState.SetCharset; // consume one more byte
                else if (b == '7') // DECSC — save cursor
                    { SaveCursor(); _state = ParseState.Ground; }
                else if (b == '8') // DECRC — restore cursor
                    { RestoreCursor(); _state = ParseState.Ground; }
                else if (b == 'M') // Reverse index
                    { ReverseIndex(); _state = ParseState.Ground; }
                else if (b == 'c') // RIS (full reset)
                    { Reset(); _state = ParseState.Ground; }
                else
                    _state = ParseState.Ground;
                break;

            case ParseState.Csi:
                if (b >= 0x30 && b <= 0x3F) // parameter bytes
                    _escParams.Append((char)b);
                else if (b >= 0x20 && b <= 0x2F) // intermediate
                    _escParams.Append((char)b);
                else // final byte
                {
                    HandleCsi((char)b);
                    _state = ParseState.Ground;
                }
                break;

            case ParseState.Osc:
                if (b == 0x07 || b == 0x1B) // BEL or ESC terminates OSC
                    _state = b == 0x1B ? ParseState.OscEscEnd : ParseState.Ground;
                // else swallow OSC content (window title, etc.)
                break;

            case ParseState.OscEscEnd:
                _state = ParseState.Ground; // consume the \ after ESC in ST
                break;

            case ParseState.SetCharset:
                _state = ParseState.Ground; // consume charset designation byte
                break;
        }
    }

    private void HandleCsi(char final)
    {
        var pStr = _escParams.ToString();
        var parts = pStr.Split(';', StringSplitOptions.None);

        switch (final)
        {
            case 'A': // Cursor Up
                _cursorRow = Math.Max(0, _cursorRow - GetParam(parts, 0, 1));
                break;
            case 'B': // Cursor Down
                _cursorRow = Math.Min(_rows - 1, _cursorRow + GetParam(parts, 0, 1));
                break;
            case 'C': // Cursor Forward
                _cursorCol = Math.Min(_cols - 1, _cursorCol + GetParam(parts, 0, 1));
                break;
            case 'D': // Cursor Back
                _cursorCol = Math.Max(0, _cursorCol - GetParam(parts, 0, 1));
                break;
            case 'H': // Cursor Position (1-based)
            case 'f':
                _cursorRow = Math.Clamp(GetParam(parts, 0, 1) - 1, 0, _rows - 1);
                _cursorCol = Math.Clamp(GetParam(parts, 1, 1) - 1, 0, _cols - 1);
                break;
            case 'J': // Erase in Display
                EraseDisplay(GetParam(parts, 0, 0));
                break;
            case 'K': // Erase in Line
                EraseLine(GetParam(parts, 0, 0));
                break;
            case 'L': // Insert lines
                InsertLines(GetParam(parts, 0, 1));
                break;
            case 'M': // Delete lines
                DeleteLines(GetParam(parts, 0, 1));
                break;
            case 'P': // Delete characters
                DeleteChars(GetParam(parts, 0, 1));
                break;
            case 'S': // Scroll Up
                ScrollUp(GetParam(parts, 0, 1));
                break;
            case 'T': // Scroll Down
                ScrollDown(GetParam(parts, 0, 1));
                break;
            case 'd': // VPA — cursor row absolute (1-based)
                _cursorRow = Math.Clamp(GetParam(parts, 0, 1) - 1, 0, _rows - 1);
                break;
            case 'G': // CHA — cursor column absolute (1-based)
                _cursorCol = Math.Clamp(GetParam(parts, 0, 1) - 1, 0, _cols - 1);
                break;
            case 'm': // SGR — Set Graphics Rendition
                HandleSgr(parts);
                break;
            case 'r': // DECSTBM — set scroll region
                _scrollTop = GetParam(parts, 0, 1) - 1;
                _scrollBottom = GetParam(parts, 1, _rows) - 1;
                _cursorRow = 0;
                _cursorCol = 0;
                break;
            case 'h': // Set Mode
                if (pStr == "?25") _cursorVisible = true;
                else if (pStr == "?1049") SwitchToAltBuffer();
                else if (pStr == "?1") _applicationCursorKeys = true;
                else if (pStr == "?2004") _bracketedPasteMode = true;
                break;
            case 'l': // Reset Mode
                if (pStr == "?25") _cursorVisible = false;
                else if (pStr == "?1049") SwitchToMainBuffer();
                else if (pStr == "?1") _applicationCursorKeys = false;
                else if (pStr == "?2004") _bracketedPasteMode = false;
                break;
            case '@': // Insert characters
                InsertChars(GetParam(parts, 0, 1));
                break;
            case 'X': // Erase characters
                EraseChars(GetParam(parts, 0, 1));
                break;
        }
    }

    private void HandleSgr(string[] parts)
    {
        if (parts.Length == 0 || (parts.Length == 1 && string.IsNullOrEmpty(parts[0])))
        {
            _currentAttr = new TerminalAttributes();
            return;
        }

        int i = 0;
        while (i < parts.Length)
        {
            int code = int.TryParse(parts[i], out var v) ? v : 0;
            switch (code)
            {
                case 0: _currentAttr = new TerminalAttributes(); break;
                case 1: _currentAttr.Bold = true; break;
                case 2: _currentAttr.Dim = true; break;
                case 3: _currentAttr.Italic = true; break;
                case 4: _currentAttr.Underline = true; break;
                case 7: _currentAttr.Inverse = true; break;
                case 9: _currentAttr.Strikethrough = true; break;
                case 22: _currentAttr.Bold = false; _currentAttr.Dim = false; break;
                case 23: _currentAttr.Italic = false; break;
                case 24: _currentAttr.Underline = false; break;
                case 27: _currentAttr.Inverse = false; break;
                case 29: _currentAttr.Strikethrough = false; break;
                case >= 30 and <= 37: _currentAttr.FgColor = (byte)(code - 30); _currentAttr.FgCustom = null; break;
                case 38: // Extended foreground
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        _currentAttr.FgColor = byte.TryParse(parts[i + 2], out var fg) ? fg : (byte)7;
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        byte.TryParse(parts[i + 2], out var r);
                        byte.TryParse(parts[i + 3], out var g);
                        byte.TryParse(parts[i + 4], out var b);
                        _currentAttr.FgCustom = Color.FromRgb(r, g, b);
                        i += 4;
                    }
                    break;
                case 39: _currentAttr.FgColor = 7; _currentAttr.FgCustom = null; break; // Default fg
                case >= 40 and <= 47: _currentAttr.BgColor = (byte)(code - 40); _currentAttr.BgCustom = null; break;
                case 48: // Extended background
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        _currentAttr.BgColor = byte.TryParse(parts[i + 2], out var bg) ? bg : (byte)0;
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        byte.TryParse(parts[i + 2], out var r);
                        byte.TryParse(parts[i + 3], out var g);
                        byte.TryParse(parts[i + 4], out var b);
                        _currentAttr.BgCustom = Color.FromRgb(r, g, b);
                        i += 4;
                    }
                    break;
                case 49: _currentAttr.BgColor = 0; _currentAttr.BgCustom = null; break; // Default bg
                case >= 90 and <= 97: _currentAttr.FgColor = (byte)(code - 90 + 8); _currentAttr.FgCustom = null; break;
                case >= 100 and <= 107: _currentAttr.BgColor = (byte)(code - 100 + 8); _currentAttr.BgCustom = null; break;
            }
            i++;
        }
    }

    private void PutChar(char ch)
    {
        if (_cursorCol >= _cols)
        {
            _cursorCol = 0;
            LineFeed();
        }
        _buffer[_cursorRow, _cursorCol] = new TerminalCell { Char = ch, Attr = _currentAttr };
        _cursorCol++;
    }

    private void LineFeed()
    {
        if (_cursorRow == _scrollBottom)
            ScrollUp(1);
        else if (_cursorRow < _rows - 1)
            _cursorRow++;
    }

    private void ReverseIndex()
    {
        if (_cursorRow == _scrollTop)
            ScrollDown(1);
        else if (_cursorRow > 0)
            _cursorRow--;
    }

    private void ScrollUp(int n)
    {
        for (int i = 0; i < n; i++)
        {
            // Capture the top row into scrollback (only for main buffer, full-screen scroll region)
            if (!_usingAltBuffer && _scrollTop == 0)
            {
                var savedLine = new TerminalCell[_cols];
                for (int c = 0; c < _cols; c++)
                    savedLine[c] = _buffer[0, c];
                _scrollback.Add(savedLine);

                // Trim scrollback if it exceeds the limit
                if (_scrollback.Count > MaxScrollbackLines)
                    _scrollback.RemoveAt(0);
            }

            for (int r = _scrollTop; r < _scrollBottom; r++)
                for (int c = 0; c < _cols; c++)
                    _buffer[r, c] = _buffer[r + 1, c];
            for (int c = 0; c < _cols; c++)
                _buffer[_scrollBottom, c] = new TerminalCell { Char = ' ' };
        }
    }

    private void ScrollDown(int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int r = _scrollBottom; r > _scrollTop; r--)
                for (int c = 0; c < _cols; c++)
                    _buffer[r, c] = _buffer[r - 1, c];
            for (int c = 0; c < _cols; c++)
                _buffer[_scrollTop, c] = new TerminalCell { Char = ' ' };
        }
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end
                for (int c = _cursorCol; c < _cols; c++)
                    _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
                for (int r = _cursorRow + 1; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = new TerminalCell { Char = ' ' };
                break;
            case 1: // start to cursor
                for (int r = 0; r < _cursorRow; r++)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = new TerminalCell { Char = ' ' };
                for (int c = 0; c <= _cursorCol; c++)
                    _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
                break;
            case 2: // entire display
            case 3:
                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = new TerminalCell { Char = ' ' };
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0: // cursor to end of line
                for (int c = _cursorCol; c < _cols; c++)
                    _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
                break;
            case 1: // start to cursor
                for (int c = 0; c <= _cursorCol; c++)
                    _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
                break;
            case 2: // entire line
                for (int c = 0; c < _cols; c++)
                    _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
                break;
        }
    }

    private void InsertLines(int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int r = _scrollBottom; r > _cursorRow; r--)
                for (int c = 0; c < _cols; c++)
                    _buffer[r, c] = _buffer[r - 1, c];
            for (int c = 0; c < _cols; c++)
                _buffer[_cursorRow, c] = new TerminalCell { Char = ' ' };
        }
    }

    private void DeleteLines(int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int r = _cursorRow; r < _scrollBottom; r++)
                for (int c = 0; c < _cols; c++)
                    _buffer[r, c] = _buffer[r + 1, c];
            for (int c = 0; c < _cols; c++)
                _buffer[_scrollBottom, c] = new TerminalCell { Char = ' ' };
        }
    }

    private void DeleteChars(int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int c = _cursorCol; c < _cols - 1; c++)
                _buffer[_cursorRow, c] = _buffer[_cursorRow, c + 1];
            _buffer[_cursorRow, _cols - 1] = new TerminalCell { Char = ' ' };
        }
    }

    private void InsertChars(int n)
    {
        for (int i = 0; i < n; i++)
        {
            for (int c = _cols - 1; c > _cursorCol; c--)
                _buffer[_cursorRow, c] = _buffer[_cursorRow, c - 1];
            _buffer[_cursorRow, _cursorCol] = new TerminalCell { Char = ' ' };
        }
    }

    private void EraseChars(int n)
    {
        for (int i = 0; i < n && _cursorCol + i < _cols; i++)
            _buffer[_cursorRow, _cursorCol + i] = new TerminalCell { Char = ' ' };
    }

    private void SaveCursor()
    {
        _savedCursorRow = _cursorRow;
        _savedCursorCol = _cursorCol;
        _savedAttr = _currentAttr;
    }

    private void RestoreCursor()
    {
        _cursorRow = Math.Clamp(_savedCursorRow, 0, _rows - 1);
        _cursorCol = Math.Clamp(_savedCursorCol, 0, _cols - 1);
        _currentAttr = _savedAttr;
    }

    private void SwitchToAltBuffer()
    {
        if (_usingAltBuffer) return;
        _altBuffer = _buffer;
        _buffer = new TerminalCell[_rows, _cols];
        InitBuffer(_buffer);
        _usingAltBuffer = true;
        _cursorRow = 0;
        _cursorCol = 0;
    }

    private void SwitchToMainBuffer()
    {
        if (!_usingAltBuffer || _altBuffer is null) return;
        _buffer = _altBuffer;
        _altBuffer = null;
        _usingAltBuffer = false;
        _cursorVisible = true; // restore cursor on return to main buffer
    }

    private void Reset()
    {
        _currentAttr = new TerminalAttributes();
        _cursorRow = 0;
        _cursorCol = 0;
        _scrollTop = 0;
        _scrollBottom = _rows - 1;
        _cursorVisible = true;
        _usingAltBuffer = false;
        _altBuffer = null;
        _bracketedPasteMode = false;
        _applicationCursorKeys = false;
        _utf8Remaining = 0;
        InitBuffer(_buffer);
    }

    private static void InitBuffer(TerminalCell[,] buf)
    {
        for (int r = 0; r < buf.GetLength(0); r++)
            for (int c = 0; c < buf.GetLength(1); c++)
                buf[r, c] = new TerminalCell { Char = ' ' };
    }

    private static int GetParam(string[] parts, int index, int defaultValue)
    {
        if (index >= parts.Length || !int.TryParse(parts[index], out var val) || val == 0)
            return defaultValue;
        return val;
    }

    private enum ParseState
    {
        Ground,
        Escape,
        Csi,
        Osc,
        OscEscEnd,
        SetCharset
    }
}

public struct TerminalCell
{
    public char Char;
    public TerminalAttributes Attr;
}

public struct TerminalAttributes
{
    public byte FgColor; // 0-15 standard, default 7 (white)
    public byte BgColor; // 0-15 standard, default 0 (black)
    public Color? FgCustom; // 24-bit RGB override
    public Color? BgCustom; // 24-bit RGB override
    public bool Bold;
    public bool Dim;
    public bool Italic;
    public bool Underline;
    public bool Inverse;
    public bool Strikethrough;

    public TerminalAttributes()
    {
        FgColor = 7;
        BgColor = 0;
    }
}
