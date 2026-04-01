using System.Diagnostics;
using System.Runtime.InteropServices;

namespace App.TaskSequencer.Client.Desktop.Maui.Platforms.Windows;

/// <summary>
/// Helper for Windows platform initialization with resource loading error handling.
/// </summary>
internal static class WindowsInitializationHelper
{
    /// <summary>
    /// Initializes Windows app with error suppression for resource loading COM errors.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            // Suppress COM error dialogs at the process level
            // SEM_NOGPFAULTERRORBOX = 0x0004
            SetErrorMode(GetErrorMode() | 0x0004);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set error mode: {ex.Message}");
            // Non-critical, continue anyway
        }
    }

    /// <summary>
    /// Safely attempts to initialize MAUI, catching and handling resource loading errors.
    /// </summary>
    public static MauiApp TryCreateMauiApp(Func<MauiApp> factory)
    {
        try
        {
            Debug.WriteLine("Attempting MAUI app creation...");
            var app = factory();
            Debug.WriteLine("MAUI app created successfully");
            return app;
        }
        catch (COMException comEx) when (comEx.HResult == unchecked((int)0x80004005))
        {
            // This exception is expected if MAUI resources aren't available
            // The CreateMauiApp method will handle the fallback
            Debug.WriteLine($"COM Exception (0x80004005) caught in TryCreateMauiApp: {comEx.Message}");
            Debug.WriteLine("Allowing factory method to handle fallback...");
            throw; // Let CreateMauiApp handle the recovery
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unexpected exception in TryCreateMauiApp: {ex}");
            throw;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetErrorMode(uint uMode);

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();
}
