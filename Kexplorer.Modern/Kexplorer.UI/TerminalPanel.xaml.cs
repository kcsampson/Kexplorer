using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kexplorer.UI.Terminal;

namespace Kexplorer.UI;

public partial class TerminalPanel : UserControl
{
    private ConPtyTerminal? _terminal;
    private Vt100Parser? _parser;
    private CancellationTokenSource? _readCts;
    private string _shellCommand = "wsl.exe";
    private string? _initialDirectory;

    // Rendering
    private WriteableBitmap? _bitmap;
    private double _cellWidth;
    private double _cellHeight;
    private int _termCols = 80;
    private int _termRows = 24;
    private Typeface _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private double _fontSize = 14;
    private double _dpiX = 96;
    private double _dpiY = 96;
    private double _pixelsPerDip = 1.0;

    // Tab-switch state tracking
    private bool _initialized;
    private bool _renderPending;
    private bool _suppressResize; // suppress PTY resize during tab-switch re-layout

    // Scrollback
    private int _scrollOffset; // 0 = at bottom (live), >0 = scrolled up into history
    private bool _updatingScrollBar; // prevent re-entrant ValueChanged

    // Mouse text selection
    private bool _isSelecting;
    private bool _hasSelection;
    private (int row, int col) _selStart; // absolute row coords
    private (int row, int col) _selEnd;
    private DateTime _lastRightClick; // debounce right-click

    // Standard 16-color ANSI palette — brightened 30% for dark skins
    private static readonly Color[] AnsiColors = new Color[]
    {
        Color.FromRgb(0x33, 0x33, 0x33), // 0 Black (not pure black — visible on dark bg)
        Color.FromRgb(0xDD, 0x33, 0x33), // 1 Red
        Color.FromRgb(0x33, 0xDD, 0x33), // 2 Green
        Color.FromRgb(0xDD, 0x88, 0x33), // 3 Yellow/Brown
        Color.FromRgb(0x33, 0x55, 0xDD), // 4 Blue
        Color.FromRgb(0xDD, 0x33, 0xDD), // 5 Magenta
        Color.FromRgb(0x33, 0xDD, 0xDD), // 6 Cyan
        Color.FromRgb(0xDD, 0xDD, 0xDD), // 7 White
        Color.FromRgb(0x77, 0x77, 0x77), // 8 Bright Black
        Color.FromRgb(0xFF, 0x77, 0x77), // 9 Bright Red
        Color.FromRgb(0x77, 0xFF, 0x77), // 10 Bright Green
        Color.FromRgb(0xFF, 0xFF, 0x77), // 11 Bright Yellow
        Color.FromRgb(0x77, 0x77, 0xFF), // 12 Bright Blue
        Color.FromRgb(0xFF, 0x77, 0xFF), // 13 Bright Magenta
        Color.FromRgb(0x77, 0xFF, 0xFF), // 14 Bright Cyan
        Color.FromRgb(0xFF, 0xFF, 0xFF), // 15 Bright White
    };

    public TerminalPanel()
    {
        InitializeComponent();
        Loaded += TerminalPanel_Loaded;
        IsVisibleChanged += TerminalPanel_IsVisibleChanged;
        SizeChanged += TerminalPanel_SizeChanged;
        PreviewKeyDown += TerminalPanel_PreviewKeyDown;
        PreviewTextInput += TerminalPanel_PreviewTextInput;
        PreviewMouseWheel += TerminalPanel_PreviewMouseWheel;
        MouseDown += TerminalPanel_MouseDown;
        MouseMove += TerminalPanel_MouseMove;
        MouseUp += TerminalPanel_MouseUp;
    }

    /// <summary>
    /// Initialize the terminal with a shell command.
    /// </summary>
    public async Task InitializeAsync(string shellCommand = "wsl.exe", string? initialDirectory = null)
    {
        // Guard against re-initialization (WPF TabControl can fire Loaded multiple times)
        if (_initialized) return;

        _shellCommand = shellCommand;
        _initialDirectory = initialDirectory;

        // Update label based on shell
        if (shellCommand.Contains("wsl", StringComparison.OrdinalIgnoreCase))
            ShellLabel.Text = "Terminal (bash)";
        else if (shellCommand.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                 shellCommand.Contains("pwsh", StringComparison.OrdinalIgnoreCase))
            ShellLabel.Text = "Terminal (PowerShell)";
        else if (shellCommand.Contains("cmd", StringComparison.OrdinalIgnoreCase))
            ShellLabel.Text = "Terminal (cmd)";
        else
            ShellLabel.Text = $"Terminal ({System.IO.Path.GetFileNameWithoutExtension(shellCommand)})";

        await StartTerminalAsync();
    }

    public string ShellCommand => _shellCommand;
    public string? InitialDirectory => _initialDirectory;

    private void TerminalPanel_Loaded(object sender, RoutedEventArgs e)
    {
        // Measure cell size from the theme font (only on first load)
        if (_cellWidth < 1)
            MeasureCellSize();

        if (_initialized)
        {
            // Tab switch back — re-render the existing buffer.
            // _suppressResize was already set to true when tab became invisible.
            _renderPending = false;
            CreateBitmap();
            RenderBuffer();

            // Allow real resizes again well after layout settles
            Dispatcher.InvokeAsync(() => _suppressResize = false,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        Focus();
    }

    private void TerminalPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_initialized)
        {
            if (!(bool)e.NewValue)
            {
                // Tab is becoming hidden — lock down resize NOW so that when
                // we come back, no SizeChanged event can trigger a PTY resize
                // before Loaded has a chance to re-render the buffer.
                _suppressResize = true;
            }
            else
            {
                // Tab becoming visible — re-render existing buffer
                _renderPending = false;
                RenderBuffer();
                Focus();
            }
        }
    }

    private void MeasureCellSize()
    {
        // Use the theme GridFontFamily or fall back to Consolas
        var fontFamily = TryFindResource("GridFontFamily") as FontFamily ?? new FontFamily("Consolas");
        _typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var fontSizeObj = TryFindResource("GridFontSize");
        _fontSize = (fontSizeObj is double fs ? fs : 13.0) + 2; // slightly larger than grid text

        // Get DPI — guard against being called when not in visual tree
        double pixelsPerDip = 1.0;
        try
        {
            var dpiInfo = VisualTreeHelper.GetDpi(this);
            _dpiX = dpiInfo.PixelsPerInchX;
            _dpiY = dpiInfo.PixelsPerInchY;
            pixelsPerDip = dpiInfo.PixelsPerDip;
            _pixelsPerDip = pixelsPerDip;
        }
        catch { /* not yet in visual tree — use defaults */ }

        // Measure a reference character
        var formatted = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _fontSize,
            Brushes.White, new NumberSubstitution(), TextFormattingMode.Display,
            pixelsPerDip);

        _cellWidth = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
        _cellHeight = Math.Ceiling(formatted.Height);
    }

    private async Task StartTerminalAsync()
    {
        if (_cellWidth < 1) MeasureCellSize();

        RecalcTerminalSize();

        _parser = new Vt100Parser(_termCols, _termRows);
        _parser.BufferChanged += OnBufferChanged;

        _terminal = new ConPtyTerminal();
        try
        {
            _terminal.Start(_shellCommand, (short)_termCols, (short)_termRows, _initialDirectory);
        }
        catch (Exception ex)
        {
            ShellLabel.Text = $"Terminal (failed: {ex.Message})";
            return;
        }

        UpdateSizeLabel();
        CreateBitmap();
        RenderBuffer();

        _readCts = new CancellationTokenSource();
        _ = ReadOutputAsync(_readCts.Token);

        _initialized = true;
        await Task.CompletedTask;
    }

    private void RecalcTerminalSize()
    {
        var border = TerminalImage.Parent as Border;
        var availWidth = (border?.ActualWidth ?? ActualWidth) - 4; // account for border
        var availHeight = (border?.ActualHeight ?? ActualHeight) - 40; // toolbar height

        if (availWidth < 1 || availHeight < 1 || _cellWidth < 1 || _cellHeight < 1)
            return;

        var newCols = Math.Max(20, (int)(availWidth / _cellWidth));
        var newRows = Math.Max(5, (int)(availHeight / _cellHeight));

        if (newCols != _termCols || newRows != _termRows)
        {
            _termCols = newCols;
            _termRows = newRows;
            _parser?.Resize(_termCols, _termRows);
            _terminal?.Resize((short)_termCols, (short)_termRows);
            UpdateSizeLabel();
            CreateBitmap();
        }
    }

    private void UpdateSizeLabel()
    {
        SizeLabel.Text = $"{_termCols}×{_termRows}";
    }

    private void TerminalPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_terminal is null || _parser is null) return;
        if (_suppressResize) return; // ignore layout changes during tab-switch
        RecalcTerminalSize();
        RenderBuffer();
    }

    #region PTY Output Reading

    private async Task ReadOutputAsync(CancellationToken ct)
    {
        if (_terminal is null) return;

        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read = await _terminal.Reader.ReadAsync(buf, ct);
                if (read == 0) break; // process exited

                _parser?.Feed(buf.AsSpan(0, read));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Process exited or pipe broken
        }

        Dispatcher.Invoke(() => ShellLabel.Text += " (exited)");
    }

    #endregion

    #region Mouse Selection

    private (int row, int col) HitTestCell(Point pos)
    {
        if (_cellWidth < 1 || _cellHeight < 1 || _parser is null)
            return (0, 0);

        // pos is relative to the TerminalImage; account for border margin
        var col = Math.Clamp((int)(pos.X / _cellWidth), 0, _parser.Cols - 1);
        var screenRow = Math.Clamp((int)(pos.Y / _cellHeight), 0, _parser.Rows - 1);

        // Convert screen row to absolute row (scrollback-aware)
        var firstAbsRow = _parser.ScrollbackCount - _scrollOffset;
        return (firstAbsRow + screenRow, col);
    }

    private void TerminalPanel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.RightButton == MouseButtonState.Pressed)
        {
            // Debounce — ignore rapid duplicate right-clicks
            var now = DateTime.UtcNow;
            if ((now - _lastRightClick).TotalMilliseconds < 300)
            {
                e.Handled = true;
                return;
            }
            _lastRightClick = now;

            if (_hasSelection)
            {
                // Right-click with selection → copy to clipboard, clear selection
                var text = GetSelectedText();
                if (!string.IsNullOrEmpty(text))
                    Clipboard.SetText(text);
                ClearSelection();
                RenderBuffer();
            }
            else if (_terminal is not null && Clipboard.ContainsText())
            {
                // Right-click without selection → paste from clipboard
                var text = Clipboard.GetText();
                SendPasteText(text);
            }
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed) return;

        Focus();
        var pos = e.GetPosition(TerminalImage);
        var cell = HitTestCell(pos);

        _selStart = cell;
        _selEnd = cell;
        _isSelecting = true;
        _hasSelection = false;
        CaptureMouse();
        RenderBuffer(); // clear any previous selection highlight
        e.Handled = true;
    }

    private void TerminalPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var pos = e.GetPosition(TerminalImage);
        _selEnd = HitTestCell(pos);
        _hasSelection = _selStart != _selEnd;
        RenderBuffer();
    }

    private void TerminalPanel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;
        ReleaseMouseCapture();

        if (!_hasSelection)
        {
            // Click without drag — clear selection
            _hasSelection = false;
            RenderBuffer();
        }
    }

    /// <summary>
    /// Get the normalized selection range (start ≤ end in reading order).
    /// </summary>
    private ((int row, int col) start, (int row, int col) end) GetNormalizedSelection()
    {
        var s = _selStart;
        var e = _selEnd;
        if (s.row > e.row || (s.row == e.row && s.col > e.col))
            (s, e) = (e, s);
        return (s, e);
    }

    private bool IsCellSelected(int absRow, int col)
    {
        if (!_hasSelection) return false;
        var (s, e) = GetNormalizedSelection();

        // Line-based selection (like most terminals)
        if (absRow < s.row || absRow > e.row) return false;
        if (absRow == s.row && absRow == e.row) return col >= s.col && col <= e.col;
        if (absRow == s.row) return col >= s.col;
        if (absRow == e.row) return col <= e.col;
        return true; // middle row — fully selected
    }

    private string GetSelectedText()
    {
        if (!_hasSelection || _parser is null) return "";

        var (s, e) = GetNormalizedSelection();
        var sb = new StringBuilder();

        for (int absRow = s.row; absRow <= e.row; absRow++)
        {
            var colStart = absRow == s.row ? s.col : 0;
            var colEnd = absRow == e.row ? e.col : _parser.Cols - 1;

            var line = new StringBuilder();
            for (int c = colStart; c <= colEnd; c++)
            {
                var ch = _parser.GetCellAbsolute(absRow, c).Char;
                line.Append(ch == '\0' ? ' ' : ch);
            }

            if (absRow < e.row)
                sb.AppendLine(line.ToString().TrimEnd());
            else
                sb.Append(line.ToString().TrimEnd());
        }

        return sb.ToString();
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _isSelecting = false;
    }

    #endregion

    #region Input Handling

    private void TerminalPanel_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_terminal is null) return;
        var bytes = Encoding.UTF8.GetBytes(e.Text);
        _terminal.Writer.Write(bytes);
        _terminal.Writer.Flush();
        e.Handled = true;
    }

    private void TerminalPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_terminal is null) return;

        // Ctrl+C with active selection → copy to clipboard (don't send ^C to shell)
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && _hasSelection)
        {
            var text = GetSelectedText();
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
            ClearSelection();
            RenderBuffer();
            e.Handled = true;
            return;
        }

        // Ctrl+V → paste from clipboard
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                SendPasteText(text);
            }
            e.Handled = true;
            return;
        }

        // Any other key clears selection
        if (_hasSelection)
        {
            ClearSelection();
            RenderBuffer();
        }

        // Application cursor keys (DECCKM ?1) — arrows send ESC O x instead of ESC [ x
        var appCursor = _parser?.ApplicationCursorKeys == true;
        string? seq = e.Key switch
        {
            Key.Enter => "\r",
            Key.Back => "\x7f",
            Key.Tab => "\t",
            Key.Escape => "\x1b",
            Key.Up => appCursor ? "\x1bOA" : "\x1b[A",
            Key.Down => appCursor ? "\x1bOB" : "\x1b[B",
            Key.Right => appCursor ? "\x1bOC" : "\x1b[C",
            Key.Left => appCursor ? "\x1bOD" : "\x1b[D",
            Key.Home => "\x1b[H",
            Key.End => "\x1b[F",
            Key.Insert => "\x1b[2~",
            Key.Delete => "\x1b[3~",
            Key.PageUp => "\x1b[5~",
            Key.PageDown => "\x1b[6~",
            Key.F1 => "\x1bOP",
            Key.F2 => "\x1bOQ",
            Key.F3 => "\x1bOR",
            Key.F4 => "\x1bOS",
            Key.F5 => "\x1b[15~",
            Key.F6 => "\x1b[17~",
            Key.F7 => "\x1b[18~",
            Key.F8 => "\x1b[19~",
            Key.F9 => "\x1b[20~",
            Key.F10 => "\x1b[21~",
            Key.F11 => "\x1b[23~",
            Key.F12 => "\x1b[24~",
            _ => null
        };

        // Ctrl+key combinations
        if (seq is null && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var ch = KeyToChar(e.Key);
            if (ch >= 'a' && ch <= 'z')
            {
                seq = ((char)(ch - 'a' + 1)).ToString(); // Ctrl+A = 0x01, etc.
            }
        }

        if (seq is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(seq);
            _terminal.Writer.Write(bytes);
            _terminal.Writer.Flush();
            e.Handled = true;
        }
    }

    private static char KeyToChar(Key key)
    {
        return key switch
        {
            >= Key.A and <= Key.Z => (char)('a' + (key - Key.A)),
            _ => '\0'
        };
    }

    #endregion

    #region Rendering

    private void CreateBitmap()
    {
        var pixelWidth = (int)(_termCols * _cellWidth);
        var pixelHeight = (int)(_termRows * _cellHeight);
        if (pixelWidth < 1 || pixelHeight < 1) return;

        _bitmap = new WriteableBitmap(pixelWidth, pixelHeight, _dpiX, _dpiY, PixelFormats.Pbgra32, null);
        TerminalImage.Source = _bitmap;
        TerminalImage.Width = pixelWidth;
        TerminalImage.Height = pixelHeight;
    }

    private void OnBufferChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible)
            {
                // Tab is hidden — just mark that we need a repaint when visible
                _renderPending = true;
                return;
            }

            // Auto-scroll to bottom when new output arrives (if already at bottom)
            if (_scrollOffset == 0)
                UpdateScrollBar();

            RenderBuffer();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void UpdateScrollBar()
    {
        if (_parser is null) return;

        _updatingScrollBar = true;
        var maxScroll = _parser.ScrollbackCount;
        TerminalScrollBar.Maximum = maxScroll;
        TerminalScrollBar.LargeChange = _termRows;
        TerminalScrollBar.ViewportSize = _termRows;

        if (_scrollOffset == 0)
            TerminalScrollBar.Value = maxScroll; // pinned to bottom

        // Show scrollbar only when there's scrollback content
        TerminalScrollBar.Visibility = maxScroll > 0 ? Visibility.Visible : Visibility.Collapsed;
        _updatingScrollBar = false;
    }

    private void TerminalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingScrollBar || _parser is null) return;

        var maxScroll = _parser.ScrollbackCount;
        _scrollOffset = Math.Max(0, maxScroll - (int)e.NewValue);
        RenderBuffer();
    }

    private void TerminalPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_parser is null) return;

        var lines = e.Delta > 0 ? -3 : 3; // scroll up = negative delta = show older lines
        var maxScroll = _parser.ScrollbackCount;
        _scrollOffset = Math.Clamp(_scrollOffset + lines, 0, maxScroll);

        _updatingScrollBar = true;
        TerminalScrollBar.Value = maxScroll - _scrollOffset;
        _updatingScrollBar = false;

        UpdateScrollBar();
        RenderBuffer();
        e.Handled = true;
    }

    private void RenderBuffer()
    {
        if (_parser is null || _bitmap is null) return;

        // Get theme colors for default fg/bg
        var themeFg = GetThemeColor("PrimaryForegroundBrush", Colors.LightGray);
        var themeBg = GetThemeColor("PrimaryBackgroundBrush", Color.FromRgb(0x0D, 0x0D, 0x0D));

        // Calculate the first absolute row to display based on scroll offset
        // When _scrollOffset == 0, show the live terminal (scrollback.Count .. scrollback.Count + rows - 1)
        // When scrolled up, shift the window back into scrollback
        var firstAbsRow = _parser.ScrollbackCount - _scrollOffset;

        // Build per-row using DrawingVisual for crisp text
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // Background fill
            dc.DrawRectangle(new SolidColorBrush(themeBg), null,
                new Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));

            for (int r = 0; r < _parser.Rows; r++)
            {
                var absRow = firstAbsRow + r;
                for (int c = 0; c < _parser.Cols; c++)
                {
                    var cell = _parser.GetCellAbsolute(absRow, c);
                    var attr = cell.Attr;

                    // Resolve colors
                    var fg = attr.FgCustom ?? (attr.FgColor == 7 ? themeFg : GetAnsiColor(attr.FgColor));
                    var bg = attr.BgCustom ?? (attr.BgColor == 0 ? themeBg : GetAnsiColor(attr.BgColor));

                    if (attr.Bold && attr.FgColor < 8 && attr.FgCustom is null)
                        fg = GetAnsiColor((byte)(attr.FgColor + 8)); // bold → bright

                    if (attr.Inverse)
                        (fg, bg) = (bg, fg);

                    if (attr.Dim)
                        fg = Color.FromArgb(128, fg.R, fg.G, fg.B);

                    var x = c * _cellWidth;
                    var y = r * _cellHeight;

                    // Draw cell background if not default
                    if (bg != themeBg)
                    {
                        dc.DrawRectangle(new SolidColorBrush(bg), null,
                            new Rect(x, y, _cellWidth, _cellHeight));
                    }

                    // Draw selection highlight
                    if (IsCellSelected(absRow, c))
                    {
                        var selColor = GetThemeColor("SelectionBrush", Color.FromRgb(0x6B, 0x5B, 0x00));
                        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(140, selColor.R, selColor.G, selColor.B)),
                            null, new Rect(x, y, _cellWidth, _cellHeight));
                        fg = GetThemeColor("SelectionForegroundBrush", Colors.White);
                    }

                    // Draw cursor (only when viewing live buffer, not scrollback)
                    if (_scrollOffset == 0 && r == _parser.CursorRow && c == _parser.CursorCol)
                    {
                        var accentColor = GetThemeColor("AccentBrush", Colors.Green);
                        if (_parser.CursorVisible)
                        {
                            // Full block cursor
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180, accentColor.R, accentColor.G, accentColor.B)),
                                null, new Rect(x, y, _cellWidth, _cellHeight));
                            fg = themeBg; // invert text on cursor
                        }
                        else
                        {
                            // Thin line cursor (always visible so user never loses their place)
                            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B)),
                                null, new Rect(x, y, 2, _cellHeight));
                        }
                    }

                    if (cell.Char != ' ' && cell.Char != '\0')
                    {
                        var weight = attr.Bold ? FontWeights.Bold : FontWeights.Normal;
                        var style = attr.Italic ? FontStyles.Italic : FontStyles.Normal;
                        var tf = new Typeface(_typeface.FontFamily, style, weight, FontStretches.Normal);

                        var ft = new FormattedText(cell.Char.ToString(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, tf, _fontSize,
                            new SolidColorBrush(fg),
                            new NumberSubstitution(), TextFormattingMode.Display,
                            _pixelsPerDip);

                        dc.DrawText(ft, new Point(x, y));
                    }

                    if (attr.Underline)
                    {
                        dc.DrawLine(new Pen(new SolidColorBrush(fg), 1),
                            new Point(x, y + _cellHeight - 1),
                            new Point(x + _cellWidth, y + _cellHeight - 1));
                    }

                    if (attr.Strikethrough)
                    {
                        var midY = y + _cellHeight / 2;
                        dc.DrawLine(new Pen(new SolidColorBrush(fg), 1),
                            new Point(x, midY),
                            new Point(x + _cellWidth, midY));
                    }
                }
            }
        }

        // Render the visual to the bitmap
        var rtb = new RenderTargetBitmap(_bitmap.PixelWidth, _bitmap.PixelHeight, _dpiX, _dpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);

        _bitmap.Lock();
        var stride = _bitmap.PixelWidth * 4;
        var pixels = new byte[stride * _bitmap.PixelHeight];
        rtb.CopyPixels(pixels, stride, 0);
        _bitmap.WritePixels(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight), pixels, stride, 0);
        _bitmap.Unlock();
    }

    private Color GetThemeColor(string resourceName, Color fallback)
    {
        if (TryFindResource(resourceName) is SolidColorBrush brush)
            return brush.Color;
        return fallback;
    }

    private static Color GetAnsiColor(byte index)
    {
        if (index < 16)
            return AnsiColors[index];

        // 256-color: 16-231 = 6x6x6 cube, 232-255 = grayscale
        if (index >= 16 && index <= 231)
        {
            var i = index - 16;
            var r = (byte)((i / 36) * 51);
            var g = (byte)(((i / 6) % 6) * 51);
            var b = (byte)((i % 6) * 51);
            return Color.FromRgb(r, g, b);
        }

        if (index >= 232)
        {
            var gray = (byte)(8 + (index - 232) * 10);
            return Color.FromRgb(gray, gray, gray);
        }

        return Colors.White;
    }

    #endregion

    #region Toolbar Actions

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_parser is null) return;

        // If there's a selection, copy just the selected text
        if (_hasSelection)
        {
            var selected = GetSelectedText();
            if (!string.IsNullOrEmpty(selected))
                Clipboard.SetText(selected);
            ClearSelection();
            RenderBuffer();
            return;
        }

        // Otherwise copy what's currently visible on screen
        var sb = new StringBuilder();
        var firstAbsRow = _parser.ScrollbackCount - _scrollOffset;
        for (int r = 0; r < _parser.Rows; r++)
        {
            var line = new StringBuilder();
            for (int c = 0; c < _parser.Cols; c++)
            {
                var ch = _parser.GetCellAbsolute(firstAbsRow + r, c).Char;
                line.Append(ch == '\0' ? ' ' : ch);
            }
            sb.AppendLine(line.ToString().TrimEnd());
        }
        var text = sb.ToString().TrimEnd();
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_terminal is null) return;
        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            SendPasteText(text);
        }
    }

    /// <summary>
    /// Send text to the terminal, wrapping in bracketed paste sequences if the mode is active.
    /// </summary>
    private void SendPasteText(string text)
    {
        if (_terminal is null) return;
        if (_parser?.BracketedPasteMode == true)
        {
            _terminal.Writer.Write(Encoding.UTF8.GetBytes("\x1b[200~"));
            _terminal.Writer.Write(Encoding.UTF8.GetBytes(text));
            _terminal.Writer.Write(Encoding.UTF8.GetBytes("\x1b[201~"));
        }
        else
        {
            _terminal.Writer.Write(Encoding.UTF8.GetBytes(text));
        }
        _terminal.Writer.Flush();
    }

    #endregion

    public async Task ShutdownAsync()
    {
        _readCts?.Cancel();
        _parser = null;

        if (_terminal is not null)
        {
            _terminal.Dispose();
            _terminal = null;
        }

        await Task.CompletedTask;
    }
}
