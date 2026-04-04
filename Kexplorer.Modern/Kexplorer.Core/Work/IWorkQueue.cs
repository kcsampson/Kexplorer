namespace Kexplorer.Core.Work;

public interface IWorkQueue : IAsyncDisposable
{
  ValueTask EnqueueAsync(IWorkItem item, CancellationToken cancellationToken = default);

  Task StartAsync(CancellationToken cancellationToken = default);

  Task StopAsync(CancellationToken cancellationToken = default);
}
