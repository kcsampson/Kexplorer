using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kexplorer.AI;
using Microsoft.Web.WebView2.Core;

namespace Kexplorer.UI;

public partial class ChatPanel : UserControl
{
    private CopilotChatService? _chatService;
    private bool _isSending;
    private bool _suppressModelChange;
    private bool _webViewReady;
    private int _messageIndex;

    // Approximate token tracking (chars / 4 heuristic)
    private int _estimatedTokensUsed;
    private int _maxContextTokens = 128_000;

    public string SelectedModel => (ModelComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "gpt-4o";

    public ChatPanel()
    {
        InitializeComponent();
        PopulateModelComboBox();
        KeyDown += ChatPanel_KeyDown;
    }

    private void PopulateModelComboBox()
    {
        _suppressModelChange = true;
        foreach (var (id, label, _) in CopilotChatService.AvailableModels)
        {
            ModelComboBox.Items.Add(new ComboBoxItem { Content = label, Tag = id });
        }
        ModelComboBox.SelectedIndex = 0;
        _suppressModelChange = false;
    }

    public async Task InitializeAsync(string? model = null)
    {
        if (model is not null)
        {
            _suppressModelChange = true;
            for (int i = 0; i < ModelComboBox.Items.Count; i++)
            {
                if (ModelComboBox.Items[i] is ComboBoxItem item && (string)item.Tag == model)
                {
                    ModelComboBox.SelectedIndex = i;
                    break;
                }
            }
            _suppressModelChange = false;
        }

        _maxContextTokens = CopilotChatService.GetMaxTokensForModel(SelectedModel);

        // Initialize WebView2
        await ChatWebView.EnsureCoreWebView2Async();

        var navComplete = new TaskCompletionSource();
        ChatWebView.NavigationCompleted += (s, e) =>
        {
            _webViewReady = true;
            navComplete.TrySetResult();
        };
        ChatWebView.NavigateToString(BuildChatHtml());

        // Wait for the HTML page to fully load before proceeding
        await navComplete.Task;


        StatusLabel.Text = "Copilot Chat - connecting...";
        SendButton.IsEnabled = false;

        try
        {
            _chatService = new CopilotChatService();
            StatusLabel.Text = "Copilot Chat - starting backend...";
            await _chatService.StartAsync(SelectedModel);
            StatusLabel.Text = $"Copilot Chat - connected ({SelectedModel})";
            SendButton.IsEnabled = true;
            InputTextBox.Focus();
        }
        catch (TimeoutException tex)
        {
            StatusLabel.Text = "Copilot Chat - timed out";
            await AppendSystemMessageAsync(
                $"**Connection timed out:**\n{tex.Message}\n\n" +
                "**Troubleshooting:**\n" +
                "1. Run `gh copilot --version` in a terminal to verify the Copilot CLI works\n" +
                "2. Run `gh auth status` to verify authentication\n" +
                "3. Check that the `GitHub.Copilot.SDK` NuGet package version is compatible");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Copilot Chat - failed: {ex.Message}";
            await AppendSystemMessageAsync(
                $"**Failed to connect to GitHub Copilot:**\n{ex.Message}\n\n" +
                $"**Exception type:** `{ex.GetType().Name}`\n\n" +
                "Make sure you have the GitHub Copilot CLI installed and authenticated (`gh auth login`).");
        }

        UpdateContextGauge();
    }

    public async Task ShutdownAsync()
    {
        if (_chatService is not null)
        {
            await _chatService.DisposeAsync();
            _chatService = null;
        }
    }

    #region Theme → CSS

    private string GetThemeColor(string resourceKey, string fallback)
    {
        if (Application.Current.TryFindResource(resourceKey) is SolidColorBrush brush)
        {
            var c = brush.Color;
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return fallback;
    }

    #endregion

    #region HTML shell

    private string BuildChatHtml()
    {
        var bg = GetThemeColor("SecondaryBackgroundBrush", "#131313");
        var fg = GetThemeColor("PrimaryForegroundBrush", "#FFE500");
        var border = GetThemeColor("ThemeBorderBrush", "#333333");
        var accent = GetThemeColor("AccentBrush", "#00FF41");

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                background: {{bg}};
                color: {{fg}};
                font-family: 'Segoe UI', system-ui, sans-serif;
                font-size: 14px;
                line-height: 1.5;
                padding: 12px;
                overflow-y: auto;
            }

            .msg { margin-bottom: 12px; padding: 10px 14px; border-radius: 8px; }
            .msg-user { background: rgba(100,255,100,0.08); border-left: 3px solid #66bb6a; }
            .msg-assistant { background: rgba(100,100,255,0.08); border-left: 3px solid #5c7cfa; }
            .msg-system { background: rgba(255,60,60,0.12); border-left: 3px solid #f44336; }

            .msg-role {
                font-size: 11px;
                font-weight: 600;
                margin-bottom: 4px;
                text-transform: uppercase;
                letter-spacing: 0.5px;
            }
            .msg-user .msg-role { color: #66bb6a; }
            .msg-assistant .msg-role { color: #5c7cfa; }
            .msg-system .msg-role { color: #f44336; }

            .msg-body { word-wrap: break-word; }
            .msg-body p { margin: 0.4em 0; }
            .msg-body ul, .msg-body ol { margin: 0.4em 0 0.4em 1.5em; }

            /* Code blocks */
            .msg-body pre {
                background: rgba(255,255,255,0.05);
                border: 1px solid {{border}};
                border-radius: 6px;
                padding: 10px 12px;
                overflow-x: auto;
                margin: 0.6em 0;
                position: relative;
            }
            .msg-body code {
                font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
                font-size: 13px;
            }
            .msg-body :not(pre) > code {
                background: rgba(255,255,255,0.08);
                padding: 2px 5px;
                border-radius: 3px;
            }

            /* Tables */
            .msg-body table {
                border-collapse: collapse;
                margin: 0.6em 0;
                width: 100%;
            }
            .msg-body th, .msg-body td {
                border: 1px solid {{border}};
                padding: 6px 10px;
                text-align: left;
            }
            .msg-body th { background: rgba(255,255,255,0.05); }

            /* Blockquotes */
            .msg-body blockquote {
                border-left: 3px solid {{accent}};
                margin: 0.4em 0;
                padding: 4px 12px;
                opacity: 0.85;
            }

            /* Mermaid */
            .mermaid { margin: 0.8em 0; }

            /* Links */
            a { color: {{accent}}; }

            /* Scrollbar */
            ::-webkit-scrollbar { width: 8px; }
            ::-webkit-scrollbar-track { background: transparent; }
            ::-webkit-scrollbar-thumb { background: {{border}}; border-radius: 4px; }
        </style>

        <!-- marked.js for Markdown -->
        <script src="https://cdn.jsdelivr.net/npm/marked@15/marked.min.js"></script>
        <!-- highlight.js for syntax highlighting -->
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/styles/github-dark.min.css">
        <script src="https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11/build/highlight.min.js"></script>
        <!-- mermaid for diagrams -->
        <script src="https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js"></script>

        <script>
            // Track library availability
            var _markedReady = (typeof marked !== 'undefined');
            var _hljsReady = (typeof hljs !== 'undefined');
            var _mermaidReady = (typeof mermaid !== 'undefined');

            // Configure mermaid if available
            if (_mermaidReady) {
                mermaid.initialize({
                    startOnLoad: false,
                    theme: 'dark',
                    securityLevel: 'loose'
                });
            }

            // Configure marked with highlight.js if available
            if (_markedReady) {
                var markedOpts = { breaks: true, gfm: true };
                if (_hljsReady) {
                    markedOpts.highlight = function(code, lang) {
                        if (lang && hljs.getLanguage(lang)) {
                            return hljs.highlight(code, { language: lang }).value;
                        }
                        return hljs.highlightAuto(code).value;
                    };
                }
                marked.setOptions(markedOpts);
            }

            const chat = document.getElementById('chat');

            function escapeHtml(text) {
                var d = document.createElement('div');
                d.textContent = text;
                return d.innerHTML;
            }

            function addMessage(id, role, cssClass) {
                const div = document.createElement('div');
                div.className = 'msg ' + cssClass;
                div.id = 'msg-' + id;
                div.innerHTML = '<div class="msg-role">' + escapeHtml(role) + '</div><div class="msg-body" id="body-' + id + '"></div>';
                chat.appendChild(div);
                scrollToBottom();
            }

            function setMessageContent(id, markdown) {
                const body = document.getElementById('body-' + id);
                if (!body) return;
                body.innerHTML = renderMarkdown(markdown);
                if (_mermaidReady) renderMermaidBlocks(body);
                scrollToBottom();
            }

            function appendDelta(id, markdown) {
                const body = document.getElementById('body-' + id);
                if (!body) return;
                body.innerHTML = renderMarkdown(markdown);
                scrollToBottom();
            }

            function finalizeMessage(id, markdown) {
                const body = document.getElementById('body-' + id);
                if (!body) return;
                body.innerHTML = renderMarkdown(markdown);
                if (_mermaidReady) renderMermaidBlocks(body);
                scrollToBottom();
            }

            function clearChat() {
                chat.innerHTML = '';
            }

            function renderMarkdown(md) {
                if (!_markedReady) {
                    // Fallback: basic HTML escaping with newlines preserved
                    return '<pre style="white-space:pre-wrap;font-family:inherit;">' + escapeHtml(md) + '</pre>';
                }
                try {
                    // Convert mermaid code blocks to <div class="mermaid"> before marked processes them
                    var mermaidRegex = /```mermaid\n([\s\S]*?)```/g;
                    var withMermaid = md.replace(mermaidRegex, function(_, code) {
                        return '<div class="mermaid">' + code.trim() + '</div>';
                    });
                    return marked.parse(withMermaid);
                } catch (e) {
                    return '<pre style="white-space:pre-wrap;font-family:inherit;">' + escapeHtml(md) + '</pre>';
                }
            }

            async function renderMermaidBlocks(container) {
                if (!_mermaidReady) return;
                const blocks = container.querySelectorAll('.mermaid:not([data-processed])');
                for (const block of blocks) {
                    try {
                        const id = 'mermaid-' + Math.random().toString(36).substr(2, 9);
                        const { svg } = await mermaid.render(id, block.textContent.trim());
                        block.innerHTML = svg;
                        block.setAttribute('data-processed', 'true');
                    } catch (e) {
                        block.innerHTML = '<pre style="color:#f44336">Mermaid error: ' + e.message + '</pre>';
                        block.setAttribute('data-processed', 'true');
                    }
                }
            }

            function scrollToBottom() {
                window.scrollTo(0, document.body.scrollHeight);
            }
        </script>
        </head>
        <body>
            <div id="chat"></div>
        </body>
        </html>
        """;
    }

    #endregion

    #region WebView2 JS helpers

    private async Task ExecuteJsAsync(string js)
    {
        if (!_webViewReady)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatPanel] JS skipped (not ready): {js.Substring(0, Math.Min(js.Length, 80))}");
            return;
        }
        try
        {
            var result = await ChatWebView.ExecuteScriptAsync(js);
            System.Diagnostics.Debug.WriteLine($"[ChatPanel] JS result: {result} for: {js.Substring(0, Math.Min(js.Length, 80))}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatPanel] JS error: {ex.Message} for: {js.Substring(0, Math.Min(js.Length, 80))}");
        }
    }

    private static string JsEscape(string s)
    {
        return JsonSerializer.Serialize(s); // produces a valid JS string literal with quotes
    }

    private async Task AppendSystemMessageAsync(string text)
    {
        var id = _messageIndex++;
        await ExecuteJsAsync($"addMessage({id}, 'System', 'msg-system')");
        await ExecuteJsAsync($"setMessageContent({id}, {JsEscape(text)})");
    }

    #endregion

    #region Toolbar handlers

    private void ChatPanel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = NewChatAsync();
            e.Handled = true;
        }
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            _ = SendMessageAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        _ = SendMessageAsync();
    }

    private void NewChatButton_Click(object sender, RoutedEventArgs e)
    {
        _ = NewChatAsync();
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _estimatedTokensUsed = 0;
        _messageIndex = 0;
        await ExecuteJsAsync("clearChat()");
        UpdateContextGauge();
    }

    private async void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressModelChange || _chatService is null) return;

        var newModel = SelectedModel;
        _maxContextTokens = CopilotChatService.GetMaxTokensForModel(newModel);

        StatusLabel.Text = $"Switching to {newModel}...";
        SendButton.IsEnabled = false;

        try
        {
            _estimatedTokensUsed = 0;
            _messageIndex = 0;
            await ExecuteJsAsync("clearChat()");
            await _chatService.ResetSessionAsync(newModel);
            StatusLabel.Text = $"Copilot Chat - connected ({newModel})";
            SendButton.IsEnabled = true;
            UpdateContextGauge();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Model switch failed: {ex.Message}";
            SendButton.IsEnabled = true;
        }
    }

    #endregion

    #region Chat logic

    private async Task NewChatAsync()
    {
        if (_chatService is null) return;

        StatusLabel.Text = "Starting new chat...";
        SendButton.IsEnabled = false;

        try
        {
            _estimatedTokensUsed = 0;
            _messageIndex = 0;
            await ExecuteJsAsync("clearChat()");
            await _chatService.ResetSessionAsync();
            StatusLabel.Text = $"Copilot Chat - connected ({SelectedModel})";
            UpdateContextGauge();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"New chat failed: {ex.Message}";
        }
        finally
        {
            SendButton.IsEnabled = true;
            InputTextBox.Focus();
        }
    }

    private async Task SendMessageAsync()
    {
        if (_isSending) return;

        if (_chatService is null || !_chatService.IsStarted)
        {
            await AppendSystemMessageAsync(
                "**Not connected.** The Copilot backend is not running. Check the status bar for details.");
            return;
        }

        var text = InputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _isSending = true;
        SendButton.IsEnabled = false;
        InputTextBox.Text = "";
        StatusLabel.Text = $"Copilot Chat - sending...";

        // Track user message tokens
        _estimatedTokensUsed += EstimateTokens(text);
        UpdateContextGauge();

        // Add user message to WebView
        var userMsgId = _messageIndex++;
        var userHtml = $"<div class='msg msg-user' id='msg-{userMsgId}'><div class='msg-role'>You</div><div class='msg-body' id='body-{userMsgId}'></div></div>";
        await ChatWebView.ExecuteScriptAsync(
            $"document.getElementById('chat').insertAdjacentHTML('beforeend', {JsEscape(userHtml)});" +
            $"document.getElementById('body-{userMsgId}').textContent = {JsEscape(text)};" +
            $"window.scrollTo(0, document.body.scrollHeight);");

        // Add assistant placeholder
        var assistantMsgId = _messageIndex++;
        var assistantHtml = $"<div class='msg msg-assistant' id='msg-{assistantMsgId}'><div class='msg-role'>Copilot</div><div class='msg-body' id='body-{assistantMsgId}'><em>Thinking...</em></div></div>";
        await ChatWebView.ExecuteScriptAsync(
            $"document.getElementById('chat').insertAdjacentHTML('beforeend', {JsEscape(assistantHtml)});" +
            $"window.scrollTo(0, document.body.scrollHeight);");

        string streamedContent = "";
        bool shownFirstDelta = false;

        try
        {
            await _chatService.SendAsync(
                text,
                onDelta: delta =>
                {
                    streamedContent += delta;
                    var captured = streamedContent;
                    Dispatcher.InvokeAsync(() =>
                    {
                        StatusLabel.Text = $"Copilot Chat - streaming...";

                        // Show the first delta in a MessageBox for debugging
                        if (!shownFirstDelta)
                        {
                            shownFirstDelta = true;
                        }

                        // Use direct DOM update instead of JS functions
                        _ = ChatWebView.ExecuteScriptAsync(
                            $"var b = document.getElementById('body-{assistantMsgId}');" +
                            $"if(b) {{ b.textContent = {JsEscape(captured)}; window.scrollTo(0, document.body.scrollHeight); }}");
                    });
                },
                onComplete: fullContent =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _estimatedTokensUsed += EstimateTokens(fullContent);
                        UpdateContextGauge();
                        // Use direct DOM update
                        _ = ChatWebView.ExecuteScriptAsync(
                            $"var b = document.getElementById('body-{assistantMsgId}');" +
                            $"if(b) {{ b.textContent = {JsEscape(fullContent)}; window.scrollTo(0, document.body.scrollHeight); }}");
                    });
                });
            StatusLabel.Text = $"Copilot Chat - connected ({SelectedModel})";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Copilot Chat - error";
            await ExecuteJsAsync($"setMessageContent({assistantMsgId}, {JsEscape($"**Error:** {ex.Message}")})");
        }
        finally
        {
            _isSending = false;
            SendButton.IsEnabled = true;
            InputTextBox.Focus();
        }
    }

    #endregion

    #region Context gauge

    private void UpdateContextGauge()
    {
        var pct = _maxContextTokens > 0
            ? Math.Clamp((double)_estimatedTokensUsed / _maxContextTokens, 0, 1)
            : 0;

        var gaugeTrackWidth = 98.0;
        ContextGaugeFill.Width = pct * gaugeTrackWidth;

        if (pct > 0.85)
            ContextGaugeFill.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
        else if (pct > 0.65)
            ContextGaugeFill.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
        else
            ContextGaugeFill.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

        var tokensK = _estimatedTokensUsed / 1000.0;
        var maxK = _maxContextTokens / 1000.0;
        ContextLabel.Text = $"{pct:P0} ({tokensK:F0}k/{maxK:F0}k)";
        ContextGaugeFill.ToolTip = $"~{_estimatedTokensUsed:N0} / {_maxContextTokens:N0} tokens ({pct:P1})";
    }

    private static int EstimateTokens(string text)
    {
        return Math.Max(1, text.Length / 4);
    }

    #endregion
}
