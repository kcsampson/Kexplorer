using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Paste files from a remote clipboard (e.g., RDP session).
/// Uses Shell COM clipboard formats (FileGroupDescriptorW/FileContents) to transfer
/// actual file bytes across the RDP virtual channel, rather than just file paths.
/// </summary>
[FolderContext]
public sealed class PasteFromRemotePlugin : IFolderPlugin
{
    public string Name => "Edit - Paste from Remote";
    public string Description => "Paste files from a remote clipboard (RDP session)";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFolder(string folderPath) => true;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        await context.Shell.ReportStatusAsync("Reading remote clipboard...", cancellationToken);

        try
        {
            var count = await context.PasteClipboardContentsToFolderAsync(folderPath, cancellationToken);

            if (count == 0)
            {
                await context.Shell.ReportStatusAsync("No files were pasted.", cancellationToken);
                return;
            }

            await context.RefreshFolderAsync(folderPath, cancellationToken);
            await context.Shell.ReportStatusAsync($"Pasted {count} file(s) from remote clipboard.", cancellationToken);
        }
        catch (NotSupportedException)
        {
            await context.Shell.ReportErrorAsync("Remote clipboard paste is not supported in this context.", cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await context.Shell.ReportErrorAsync(ex.Message, cancellationToken: cancellationToken);
        }
    }
}
