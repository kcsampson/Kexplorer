namespace Kexplorer.Core.Work;

public sealed class WorkQueueOptions
{
  public int? BoundedCapacity { get; init; }

  public int WorkerCount { get; init; } = 1;
}
