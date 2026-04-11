using GitHub.Copilot.SDK;

namespace Kexplorer.AI;

/// <summary>
/// Wraps the GitHub Copilot SDK to provide a simple chat interface.
/// Call <see cref="StartAsync"/> once, then <see cref="SendAsync"/> for each message.
/// </summary>
public sealed class CopilotChatService : IAsyncDisposable
{
    private CopilotClient? _client;
    private CopilotSession? _session;
    private bool _started;
    private string _model = "gpt-4o";
    private int _handlerGeneration;

    public bool IsStarted => _started;
    public string Model => _model;

    /// <summary>
    /// Known models with their approximate max context sizes (in tokens).
    /// </summary>
    public static readonly (string Id, string Label, int MaxTokens)[] AvailableModels =
    [
        ("gpt-4o", "GPT-4o", 128_000),
        ("gpt-4o-mini", "GPT-4o Mini", 128_000),
        ("gpt-5", "GPT-5", 256_000),
        ("claude-sonnet-4", "Claude Sonnet 4", 200_000),
        ("o3-mini", "o3-mini", 200_000),
    ];

    public static int GetMaxTokensForModel(string modelId)
    {
        foreach (var m in AvailableModels)
        {
            if (m.Id == modelId) return m.MaxTokens;
        }
        return 128_000; // fallback
    }

    /// <summary>
    /// Starts the Copilot CLI backend and creates a chat session.
    /// </summary>
    public async Task StartAsync(string? model = null, int timeoutSeconds = 30)
    {
        if (model is not null)
            _model = model;

        if (_started)
        {
            // If already started, just create a new session with the new model
            await ResetSessionAsync();
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        _client = new CopilotClient();

        try
        {
            await _client.StartAsync().WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Copilot backend did not start within {timeoutSeconds}s. " +
                "Ensure 'gh copilot' works from your terminal and you are authenticated (gh auth login).");
        }

        try
        {
            _session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = _model,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
            }).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Copilot session creation timed out. The backend started but could not create a '{_model}' session.");
        }

        _started = true;
    }

    /// <summary>
    /// Disposes the current session and creates a fresh one (new conversation).
    /// </summary>
    public async Task ResetSessionAsync(string? model = null)
    {
        if (_client is null)
            throw new InvalidOperationException("Client not started.");

        if (model is not null)
            _model = model;

        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            _session = await _client.CreateSessionAsync(new SessionConfig
            {
                Model = _model,
                Streaming = true,
                OnPermissionRequest = PermissionHandler.ApproveAll,
            }).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"Session reset timed out for model '{_model}'. The Copilot backend may be unresponsive.");
        }
    }

    /// <summary>
    /// Sends a message and streams back tokens via <paramref name="onDelta"/>.
    /// <paramref name="onComplete"/> fires when the full response is available.
    /// </summary>
    public async Task SendAsync(string prompt, Action<string> onDelta, Action<string> onComplete, int timeoutSeconds = 120)
    {
        if (_session is null)
            throw new InvalidOperationException("Call StartAsync before sending messages.");

        // Bump generation so stale handlers from previous calls are ignored
        var generation = ++_handlerGeneration;
        var done = new TaskCompletionSource();
        string fullContent = "";

        _session.On(evt =>
        {
            // Ignore events if a newer SendAsync call has been made
            if (_handlerGeneration != generation) return;

            switch (evt)
            {
                case AssistantMessageDeltaEvent delta:
                    onDelta(delta.Data.DeltaContent ?? "");
                    break;
                case AssistantMessageEvent msg:
                    fullContent = msg.Data.Content ?? "";
                    break;
                case SessionIdleEvent:
                    onComplete(fullContent);
                    done.TrySetResult();
                    break;
            }
        });

        await _session.SendAsync(new MessageOptions { Prompt = prompt });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await done.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                $"No response received within {timeoutSeconds}s. The Copilot backend may be unresponsive.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _started = false;
    }
}
