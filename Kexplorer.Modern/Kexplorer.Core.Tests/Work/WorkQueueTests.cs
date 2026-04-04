using System.Collections.Concurrent;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;
using Xunit;

namespace Kexplorer.Core.Tests.Work;

public sealed class WorkQueueTests
{
  // ---------------------------------------------------------------------------
  // Test double — captures shell callback invocations for assertion.
  // ---------------------------------------------------------------------------
  private sealed class CapturingShell : IKexplorerShell
  {
    public ConcurrentBag<string> StatusMessages { get; } = [];
    public ConcurrentBag<(string Message, Exception? Exception)> Errors { get; } = [];
    public ConcurrentBag<string> RefreshedPaths { get; } = [];

    public Task ReportStatusAsync(string message, CancellationToken cancellationToken = default)
    {
      StatusMessages.Add(message);
      return Task.CompletedTask;
    }

    public Task ReportErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
      Errors.Add((message, exception));
      return Task.CompletedTask;
    }

    public Task RefreshPathAsync(string path, CancellationToken cancellationToken = default)
    {
      RefreshedPaths.Add(path);
      return Task.CompletedTask;
    }

    public Task SetTreeChildrenAsync(string parentPath, IReadOnlyList<FileSystem.FileSystemNode> children, CancellationToken cancellationToken = default)
      => Task.CompletedTask;

    public Task SetFileListAsync(string directoryPath, IReadOnlyList<FileSystem.FileEntry> files, CancellationToken cancellationToken = default)
      => Task.CompletedTask;

    public Task NavigateToPathAsync(string path, CancellationToken cancellationToken = default)
      => Task.CompletedTask;

    public Task RemoveTreeNodeAsync(string path, CancellationToken cancellationToken = default)
      => Task.CompletedTask;
  }

  // ---------------------------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------------------------
  private static WorkQueue CreateQueue(CapturingShell shell, WorkQueueOptions? options = null) =>
    new(shell, options);

  private static DelegateWorkItem CompletingItem(string name, TaskCompletionSource tcs) =>
    new(name, (_, _) =>
    {
      tcs.TrySetResult();
      return Task.CompletedTask;
    });

  // ---------------------------------------------------------------------------
  // Tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task EnqueuedItem_IsExecuted()
  {
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell);
    await queue.StartAsync();

    var tcs = new TaskCompletionSource();
    await queue.EnqueueAsync(CompletingItem("item1", tcs));

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(tcs.Task.IsCompleted);
  }

  [Fact]
  public async Task MultipleItems_AllExecuted()
  {
    const int Count = 20;
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell, new WorkQueueOptions { WorkerCount = 2 });
    await queue.StartAsync();

    var barrier = new SemaphoreSlim(0, Count);
    for (var i = 0; i < Count; i++)
    {
      var name = $"item{i}";
      await queue.EnqueueAsync(new DelegateWorkItem(name, (_, _) =>
      {
        barrier.Release();
        return Task.CompletedTask;
      }));
    }

    for (var i = 0; i < Count; i++)
    {
      Assert.True(await barrier.WaitAsync(TimeSpan.FromSeconds(10)));
    }
  }

  [Fact]
  public async Task FailingItem_ReportsErrorToShell_AndContinues()
  {
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell);
    await queue.StartAsync();

    var successTcs = new TaskCompletionSource();
    var failingItem = new DelegateWorkItem("boom", (_, _) => throw new InvalidOperationException("kaboom"));
    var successItem = CompletingItem("success", successTcs);

    await queue.EnqueueAsync(failingItem);
    await queue.EnqueueAsync(successItem);

    // The error should be reported and the queue continues to process the next item.
    await successTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

    Assert.Single(shell.Errors);
    Assert.Contains("boom", shell.Errors.First().Message);
    Assert.IsType<InvalidOperationException>(shell.Errors.First().Exception);
  }

  [Fact]
  public async Task StopAsync_CompletesGracefully()
  {
    var shell = new CapturingShell();
    var queue = CreateQueue(shell);
    await queue.StartAsync();

    var tcs = new TaskCompletionSource();
    await queue.EnqueueAsync(CompletingItem("item", tcs));

    // Wait for the item to execute before stopping.
    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await queue.StopAsync();

    // Disposing again via DisposeAsync should not throw.
    await queue.DisposeAsync();
  }

  [Fact]
  public async Task StartAsync_CalledTwice_IsIdempotent()
  {
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell);

    // Should not throw or start duplicate workers.
    await queue.StartAsync();
    await queue.StartAsync();

    var tcs = new TaskCompletionSource();
    await queue.EnqueueAsync(CompletingItem("item", tcs));
    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
  }

  [Fact]
  public async Task StopAsync_WithoutStart_DoesNotThrow()
  {
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell);
    await queue.StopAsync(); // Should not throw.
  }

  [Fact]
  public async Task BoundedQueue_AllItemsProcessed_WithBoundedCapacity()
  {
    // Verify that a bounded queue still drains all items even though the
    // channel capacity is smaller than the total number of items enqueued.
    const int Count = 10;
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell, new WorkQueueOptions { BoundedCapacity = 3, WorkerCount = 1 });
    await queue.StartAsync();

    using var latch = new CountdownEvent(Count);
    for (var i = 0; i < Count; i++)
    {
      // EnqueueAsync will back-pressure (await) if the channel is full.
      await queue.EnqueueAsync(new DelegateWorkItem($"item{i}", (_, _) =>
      {
        latch.Signal();
        return Task.CompletedTask;
      }));
    }

    Assert.True(latch.Wait(TimeSpan.FromSeconds(10)));
  }

  [Fact]
  public async Task WorkerCount_CanBeConfigured()
  {
    var shell = new CapturingShell();
    await using var queue = CreateQueue(shell, new WorkQueueOptions { WorkerCount = 4 });
    await queue.StartAsync();

    using var latch = new CountdownEvent(10);
    for (var i = 0; i < 10; i++)
    {
      await queue.EnqueueAsync(new DelegateWorkItem($"item{i}", (_, _) =>
      {
        latch.Signal();
        return Task.CompletedTask;
      }));
    }

    Assert.True(latch.Wait(TimeSpan.FromSeconds(10)));
  }

  [Fact]
  public void Constructor_ThrowsOnZeroWorkerCount()
  {
    var shell = new CapturingShell();
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      new WorkQueue(shell, new WorkQueueOptions { WorkerCount = 0 }));
  }

  [Fact]
  public async Task DelegateWorkItem_ExecutesDelegate()
  {
    var executed = false;
    var item = new DelegateWorkItem("test", (_, _) =>
    {
      executed = true;
      return Task.CompletedTask;
    });

    var shell = new CapturingShell();
    await item.ExecuteAsync(shell, CancellationToken.None);

    Assert.True(executed);
    Assert.Equal("test", item.Name);
  }

  [Fact]
  public void DelegateWorkItem_ThrowsOnNullName()
  {
    // ArgumentException.ThrowIfNullOrWhiteSpace(null) throws ArgumentNullException
    // (a subclass of ArgumentException) — match on the specific type.
    Assert.Throws<ArgumentNullException>(() =>
      new DelegateWorkItem(null!, (_, _) => Task.CompletedTask));
  }

  [Fact]
  public void DelegateWorkItem_ThrowsOnNullDelegate()
  {
    Assert.Throws<ArgumentNullException>(() =>
      new DelegateWorkItem("name", null!));
  }
}
