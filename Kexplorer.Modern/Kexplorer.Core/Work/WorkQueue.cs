using System.Threading.Channels;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

public sealed class WorkQueue : IWorkQueue
{
  private readonly IKexplorerShell shell;
  private readonly Channel<IWorkItem> channel;
  private readonly WorkQueueOptions options;
  private readonly List<Task> workers = new();
  private readonly CancellationTokenSource stopCts = new();
  private bool started;

  public WorkQueue(IKexplorerShell shell, WorkQueueOptions? options = null)
  {
    this.shell = shell;
    this.options = options ?? new WorkQueueOptions();

    if (this.options.WorkerCount <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(options), "WorkerCount must be at least 1.");
    }

    channel = this.options.BoundedCapacity is int capacity
      ? Channel.CreateBounded<IWorkItem>(new BoundedChannelOptions(capacity)
      {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = this.options.WorkerCount == 1,
        SingleWriter = false
      })
      : Channel.CreateUnbounded<IWorkItem>(new UnboundedChannelOptions
      {
        SingleReader = this.options.WorkerCount == 1,
        SingleWriter = false
      });
  }

  public async ValueTask EnqueueAsync(IWorkItem item, CancellationToken cancellationToken = default)
  {
    if (item is null)
    {
      throw new ArgumentNullException(nameof(item));
    }

    await channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
  }

  public Task StartAsync(CancellationToken cancellationToken = default)
  {
    if (started)
    {
      return Task.CompletedTask;
    }

    started = true;

    for (var i = 0; i < options.WorkerCount; i++)
    {
      workers.Add(Task.Run(() => WorkerLoopAsync(stopCts.Token), cancellationToken));
    }

    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken cancellationToken = default)
  {
    if (!started)
    {
      return;
    }

    channel.Writer.TryComplete();
    stopCts.Cancel();

    await Task.WhenAll(workers).WaitAsync(cancellationToken).ConfigureAwait(false);
    started = false;
  }

  public async ValueTask DisposeAsync()
  {
    await StopAsync().ConfigureAwait(false);
    stopCts.Dispose();
  }

  private async Task WorkerLoopAsync(CancellationToken cancellationToken)
  {
    try
    {
      // Each worker drains the queue and reports failures through the shell.
      await foreach (var workItem in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
      {
        try
        {
          await workItem.ExecuteAsync(shell, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
          return;
        }
        catch (Exception ex)
        {
          await shell.ReportErrorAsync($"Work item '{workItem.Name}' failed.", ex, cancellationToken).ConfigureAwait(false);
        }
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // Graceful exit: StopAsync signalled cancellation while the worker was
      // waiting for the next item from ReadAllAsync.  This is expected.
    }
  }
}
