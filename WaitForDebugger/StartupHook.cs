using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

/// <summary>
/// .NET startup hook, loaded through <c>DOTNET_STARTUP_HOOKS</c>, that holds the process before its
/// <c>Main</c> runs until a debugger attaches, or until
/// <c>WAIT_FOR_DEBUGGER_TIMEOUT</c> seconds (default 60) have passed.
/// </summary>
internal static class StartupHook
{
    private const int DefaultTimeoutSeconds = 60;

    /// <summary>Called by the runtime before the entry assembly's <c>Main</c>.</summary>
    public static void Initialize()
    {
        // Child processes (e.g. a DevTools test plan's) must not wait for a debugger as well.
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null);

        var timeoutSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("WAIT_FOR_DEBUGGER_TIMEOUT"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : DefaultTimeoutSeconds;

        Console.Error.WriteLine(
            $"[WaitForDebugger] Waiting up to {timeoutSeconds}s for a debugger to attach"
        );
        var stopwatch = Stopwatch.StartNew();
        while (!Debugger.IsAttached && stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            Thread.Sleep(50);
        }
        Console.Error.WriteLine(
            Debugger.IsAttached
                ? "[WaitForDebugger] Debugger attached"
                : "[WaitForDebugger] No debugger attached; continuing without one"
        );
    }
}
