using System.Windows;
using System.Windows.Threading;

namespace Kexplorer.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch unhandled exceptions on the UI thread
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // Catch unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Catch unobserved task exceptions
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            $"An error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.GetType().Name}",
            "KExplorer Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true; // Prevent app crash
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled domain exception: {ex}");
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}",
                "KExplorer Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
        e.SetObserved(); // Prevent app crash
    }
}
