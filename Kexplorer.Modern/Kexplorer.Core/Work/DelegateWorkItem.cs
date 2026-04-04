using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Wraps an async delegate as an <see cref="IWorkItem"/>.
/// Useful for quick ad-hoc work items and for wrapping ported legacy
/// <c>IWorkUnit.DoJob()</c> logic during incremental migration.
/// </summary>
/// <remarks>
/// Legacy migration pattern:
/// <code>
///   // Old (legacy IWorkUnit.DoJob):
///   //   public IWorkUnit DoJob() { LoadDirectory(path); return null; }
///
///   // New (wrapped as DelegateWorkItem):
///   var item = new DelegateWorkItem("LoadDirectory", async (shell, ct) =>
///   {
///       var entries = await Task.Run(() => Directory.GetFileSystemEntries(path), ct);
///       await shell.RefreshPathAsync(path, ct);
///   });
///   await queue.EnqueueAsync(item);
/// </code>
/// </remarks>
public sealed class DelegateWorkItem : IWorkItem
{
  private readonly Func<IKexplorerShell, CancellationToken, Task> execute;

  public DelegateWorkItem(string name, Func<IKexplorerShell, CancellationToken, Task> execute)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(execute);

    Name = name;
    this.execute = execute;
  }

  public string Name { get; }

  public Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken) =>
    execute(shell, cancellationToken);
}
