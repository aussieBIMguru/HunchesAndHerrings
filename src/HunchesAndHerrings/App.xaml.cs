using System.Windows;
using System.Windows.Threading;

namespace HunchesAndHerrings;

/// <summary>Application entry point.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep the game running on unexpected errors: show a period-appropriate note instead of a crash dialog.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went awry in the investigation:\n\n{e.Exception.Message}",
            "Hunches & Herrings",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
