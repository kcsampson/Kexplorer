using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

public interface IWorkItem
{
  string Name { get; }

  Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken);
}
