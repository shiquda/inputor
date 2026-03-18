using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Inputor.App.Services;

namespace Inputor.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--count-sample")
        {
            Console.WriteLine(CharacterCountService.CountSupportedCharacters(args[1]));
            return;
        }

        if (args.Length >= 2 && args[0] == "--simulate-sequence")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            foreach (var sample in args[1].Split('|', StringSplitOptions.None))
            {
                var result = tracker.ProcessSnapshot("sample", sample, now);
                Console.WriteLine($"{sample} => delta={result.Delta}, pending={result.IsPendingComposition}");
                now = now.AddMilliseconds(500);
            }

            return;
        }

        if (args.Length >= 4 && args[0] == "--simulate-paste")
        {
            var tracker = new CompositionAwareDeltaTracker();
            var now = DateTime.UtcNow;
            tracker.ProcessSnapshot("sample", args[1], now);
            var result = tracker.ProcessSnapshot("sample", args[2], now.AddMilliseconds(500));
            var isPaste = PasteDetectionService.LooksLikePaste(result.InsertedTextSegment, args[3]);
            Console.WriteLine($"delta={result.Delta}, pending={result.IsPendingComposition}, inserted={result.InsertedTextSegment}, paste={isPaste}");
            return;
        }

        if (args.Length >= 5 && args[0] == "--simulate-bulk")
        {
            var delta = int.Parse(args[1]);
            var inserted = args[2];
            var controlTypeName = args[3];
            var isPaste = bool.Parse(args[4]);
            Console.WriteLine(BulkLoadDetectionService.LooksLikeBulkContentLoad(delta, inserted, controlTypeName, isPaste));
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect();
}
