using System.Threading;
using Inputor.App.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Inputor.WinUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TryHandleCli(args))
        {
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        try
        {
            Application.Start(_initParams =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                var app = new App();
            });
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static bool TryHandleCli(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--count-sample")
        {
            Console.WriteLine(CharacterCountService.CountSupportedCharacters(args[1]));
            return true;
        }

        if (args.Length >= 2 && args[0] == "--simulate-sequence")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            var isNativeImeInputMode = args.Length >= 3 && bool.TryParse(args[2], out var parsedImeMode) && parsedImeMode;
            foreach (var sample in args[1].Split('|', StringSplitOptions.None))
            {
                var result = tracker.ProcessSnapshot("sample", sample, now, isNativeImeInputMode);
                Console.WriteLine($"{sample} => delta={result.Delta}, pending={result.IsPendingComposition}");
                now = now.AddMilliseconds(500);
            }

            return true;
        }

        if (args.Length >= 4 && args[0] == "--simulate-paste")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            _ = tracker.ProcessSnapshot("sample", args[1], now, false);
            var result = tracker.ProcessSnapshot("sample", args[2], now.AddMilliseconds(500), false);
            var isPaste = PasteDetectionService.LooksLikePaste(result.InsertedTextSegment, args[3]);
            Console.WriteLine($"delta={result.Delta}, pending={result.IsPendingComposition}, inserted={result.InsertedTextSegment}, paste={isPaste}");
            return true;
        }

        if (args.Length >= 5 && args[0] == "--simulate-bulk")
        {
            var delta = int.Parse(args[1]);
            var inserted = args[2];
            var controlTypeName = args[3];
            var isPaste = bool.Parse(args[4]);
            Console.WriteLine(BulkLoadDetectionService.LooksLikeBulkContentLoad(delta, inserted, controlTypeName, isPaste));
            return true;
        }

        return false;
    }
}
